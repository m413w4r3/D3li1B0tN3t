# P2N Linux X11 backend

## Runtime requirements

The only P2N desktop backend is Xorg/X11. The locator requires XDG_SESSION_TYPE=x11, a non-empty DISPLAY that
xdpyinfo can query, and the XTEST extension. Wayland is not supported by this backend.

The caller supplies a positive numeric XID. The locator never searches titles or chooses the first window whose name
contains Dofus. A title is descriptive only. X11's _NET_WM_PID property is read with xprop; when present, the
corresponding /proc/PID/exe link is read and checked against the expected executable basename. An unavailable PID or
unreadable executable remains an unverified identity. Such a window may be used for Preview when the caller explicitly
selects it, but the action gate requires Status=Verified on the current observation.

## Window state and geometry

The locator uses xwininfo -id XID to read map state, absolute origin, client dimensions, and border width. It uses
xdotool getwindowgeometry --shell to cross-check origin and client dimensions, and xdotool getwindowfocus to test
focus. xprop reads _NET_WM_PID, WM_STATE, _NET_WM_STATE, and the descriptive title; xprop -root _NET_ACTIVE_WINDOW
checks the active XID. Where _NET_ACTIVE_WINDOW is unavailable, focus is used as the active-window fallback. A window
must exist, be mapped and viewable, not be minimized, and have reliable positive geometry.

The reported total selected-XID extent is client width/height plus both X11 borders. The code measures that selected
XID and its border; it does not separately measure window-manager decoration extents outside the selected XID.
Capture dimensions must equal the measured selected-XID extent, including its border. Width and height differences
are retained in CapturedFrame and a non-zero difference fails capture rather than being assumed to be zero.

## Capture

MaimWindowCapture invokes the absolute maim executable without a shell, using argument-list elements equivalent to:

    maim -u -f png -i XID

PNG bytes arrive on stdout and are decoded in memory by OpenCvSharp. The process timeout is at most 2 seconds; stdout
is capped at 16,777,216 bytes; decoded images are capped at 3840×2160. Stderr is drained while retaining at most 64 KiB,
and failure details are sanitized and capped. Timeout and oversized output terminate the process tree and close the
parent pipes. No image is saved by the capture backend.

Decode failure, unexpected dimensions, changed window observations, constant pixels, or an image that is at least
99.5% near-black produce explicit failure statuses. A Captured result means the image passed these capture checks; it
does not mean that a scene or resource was recognized.

Capture stability compares XID, ProcessId, ExecutablePath, Status, and geometry exactly across its before and after
observations. A change in process metadata availability or identity status during capture returns WindowChanged.

## XTest input

XdotoolGuiInput is the real-input implementation. It checks that the expected process is verified, re-locates the
same XID, verifies mapping, geometry, active/focused state, X11, DISPLAY, and XTEST, and checks that the point is
inside the client area with a 2-pixel margin. It then moves the pointer with xdotool mousemove --sync X Y, re-locates
and revalidates the window immediately before sending xdotool click 1. It calls `GuiClickLease.TryBeginClick` after the
caller-token and lease-state checks and immediately before `Process.Start`. That lock-protected claim is the linearization point:
revocation that wins first prevents click launch, while a claim that wins first is treated as possibly emitted. The
click has no --window option: it is a global XTest event. This input implementation requires the expected
and current ProcessId and ExecutablePath to match exactly, and the current status to be Verified. The action gate
independently requires both compared identities to be Verified with exact Xid, ProcessId, and ExecutablePath equality.

The focus observation and global click are not atomic. A focus change in the small interval after revalidation and
before the click remains possible. P2N input tests use only a fake window in Xvfb.

`GuiInputResult.ClickMayHaveBeenEmitted` is true once the lease's launch point wins. If the command then fails, times
out, or is canceled, its result is uncertain because XTest may already have emitted the click. The action executor
reports that case as `UNCONFIRMED` and does not retry. Validation refusals and failures before the lease claim report
no possible click.

Every observable revocation cancels the active lease synchronously before actor inbox handling: pause, attach, detach,
stop, decoded client gameplay, fight start, map state changes, self movement, movement refusal/confirmation, and an
unread frame that closes the movement translation latch. The inbox then applies the same revocation to correlation as
defense in depth. After the actor's `MarkClicked` gate succeeds, a session epoch has spent its one permitted GUI click
attempt even if the backend is canceled before launching the command. A new action requires a new session epoch. If the
lease's launch claim wins before revocation, the click cannot be retracted and any later failure or cancellation is
uncertain; the action is not retried.

## Privileges and dependencies

The backend uses ordinary user-level X11 access. It does not use /dev/uinput, ydotool, root, Linux capabilities, a
root service, Frida, or injection into Unity. maim and xdotool are external runtime tools; they are not distributed
or linked by this repository. OpenCvSharp5 and its slim linux-x64 runtime are the project's NuGet dependencies.
Their versions and source commits are recorded in THIRD-PARTY-NOTICES.md.

## Xvfb evidence and limits

The final P2N test run exercised 21 X11 tests on Xvfb: 8 window locator tests from B1, 8 capture tests from B2, and
5 XTest input tests from B2; none were skipped. These tests used fake windows, not Dofus.

The tests establish behavior against Xvfb and controlled applications only. They do not establish that XTest input
works with Dofus, that a Dofus frame is visible, or that a Unity scene can be calibrated. A window manager may omit
active-window or minimization properties; an absent active property uses focus as fallback, and absent minimization
properties are reported as not minimized. Decoration behavior outside the selected XID remains a geometry risk. A
future Wayland backend would need a separate, explicitly reviewed implementation and evidence.
