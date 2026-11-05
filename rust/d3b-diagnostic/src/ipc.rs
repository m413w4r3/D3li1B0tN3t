use std::path::Path;

use crate::error::ReplayError;

#[derive(Debug)]
pub struct ServeOutput {
    pub exit_code: i32,
    pub stderr_line: String,
}

pub fn serve(path: &Path) -> Result<ServeOutput, ReplayError> {
    #[cfg(unix)]
    {
        serve_unix::serve(path)
    }
    #[cfg(not(unix))]
    {
        let _ = path;
        Err(ReplayError::contract("UNSUPPORTED_PLATFORM"))
    }
}

#[cfg(unix)]
mod serve_unix {
    use std::fs;
    use std::io::{self, Read, Write};
    use std::os::unix::fs::PermissionsExt;
    use std::os::unix::net::{UnixListener, UnixStream};
    use std::path::{Path, PathBuf};
    use std::sync::atomic::{AtomicBool, Ordering};
    use std::sync::Arc;
    use std::thread;
    use std::time::Duration;

    use serde_json::{json, Value};

    use crate::error::ReplayError;
    use crate::ipc::ServeOutput;
    use crate::replay::{analyze_frame, error_line, parse_frame_with_key, parse_hello};
    use crate::schema::model::Schema;
    use crate::summary::Summary;

    const MAX_IPC_MESSAGE: usize = 1_048_576;
    const PROTOCOL_VERSION: u64 = 1;
    const SNS_COMMIT: &str = "c780035c9020058a772ac8a2eef09555acd5a173";

    struct SocketPathGuard(PathBuf);

    impl Drop for SocketPathGuard {
        fn drop(&mut self) {
            let _ = fs::remove_file(&self.0);
        }
    }

    enum SessionEnd {
        Bye(String),
        EofBeforeHandshake,
        EofAfterHandshake(String),
    }

    pub(super) fn serve(path: &Path) -> Result<ServeOutput, ReplayError> {
        let listener =
            UnixListener::bind(path).map_err(|_| ReplayError::contract("SOCKET_FAILED"))?;
        let _path_guard = SocketPathGuard(path.to_path_buf());
        fs::set_permissions(path, fs::Permissions::from_mode(0o600))
            .map_err(|_| ReplayError::contract("SOCKET_FAILED"))?;

        let (mut stream, _) = listener
            .accept()
            .map_err(|_| ReplayError::contract("IO_ERROR"))?;
        listener
            .set_nonblocking(true)
            .map_err(|_| ReplayError::contract("SOCKET_FAILED"))?;
        let reject_connections = listener
            .try_clone()
            .map_err(|_| ReplayError::contract("SOCKET_FAILED"))?;
        let stop_rejecting = Arc::new(AtomicBool::new(false));
        let reject_flag = Arc::clone(&stop_rejecting);
        let reject_thread = thread::Builder::new()
            .name("d3b-diagnostic-reject-clients".to_owned())
            .spawn(move || reject_while_active(reject_connections, reject_flag))
            .map_err(|_| ReplayError::internal())?;

        let session = serve_session(&mut stream);
        if let Err(error) = &session {
            send_error(&mut stream, error);
        }
        stop_rejecting.store(true, Ordering::Release);
        let _ = reject_thread.join();

        match session {
            Ok(SessionEnd::Bye(stderr_line) | SessionEnd::EofAfterHandshake(stderr_line)) => {
                Ok(ServeOutput {
                    exit_code: 0,
                    stderr_line,
                })
            }
            Ok(SessionEnd::EofBeforeHandshake) => Ok(ServeOutput {
                exit_code: 2,
                stderr_line: "code=CONTRACT_INVALID".to_owned(),
            }),
            Err(error) => Err(error),
        }
    }

    fn reject_while_active(listener: UnixListener, stop: Arc<AtomicBool>) {
        while !stop.load(Ordering::Acquire) {
            match listener.accept() {
                Ok((stream, _)) => drop(stream),
                Err(error) if error.kind() == io::ErrorKind::WouldBlock => {
                    thread::sleep(Duration::from_millis(2));
                }
                Err(error) if error.kind() == io::ErrorKind::Interrupted => {}
                Err(_) => break,
            }
        }
    }

    fn serve_session(stream: &mut UnixStream) -> Result<SessionEnd, ReplayError> {
        let Some(hello_value) = read_message(stream)? else {
            return Ok(SessionEnd::EofBeforeHandshake);
        };
        require_kind(&hello_value, "hello")?;
        let hello = parse_hello(&hello_value)?;

        let Some(schema_value) = read_message(stream)? else {
            return Ok(SessionEnd::EofBeforeHandshake);
        };
        require_kind(&schema_value, "schema")?;
        let schema = Schema::parse(&schema_value).map_err(ReplayError::contract)?;

        write_message(
            stream,
            &json!({
                "kind": "hello",
                "protocolVersion": PROTOCOL_VERSION,
                "engine": "d3b-diagnostic",
                "engineVersion": env!("CARGO_PKG_VERSION"),
                "snsCommit": SNS_COMMIT,
            }),
        )?;

        let mut summary = Summary::default();
        let mut previous_sequence = None;
        while let Some(value) = read_message(stream)? {
            match value.get("kind").and_then(Value::as_str) {
                Some("frame") => {
                    let frame = parse_frame_with_key(&value, "sequence")?;
                    if previous_sequence.is_some_and(|previous| frame.index <= previous) {
                        return Err(ReplayError::contract("CONTRACT_INVALID"));
                    }
                    previous_sequence = Some(frame.index);
                    let (analysis_bytes, analysis) =
                        analyze_frame(&schema, &frame, "sequence", false)?;
                    let status = analysis
                        .get("status")
                        .and_then(Value::as_str)
                        .unwrap_or("MALFORMED");
                    let problems = analysis
                        .get("problems")
                        .and_then(Value::as_array)
                        .map(Vec::as_slice)
                        .unwrap_or(&[]);
                    summary.observe(status, problems);
                    write_message_bytes(stream, &analysis_bytes)?;
                }
                Some("bye") => {
                    if value.as_object().is_none_or(|object| object.len() != 1) {
                        return Err(ReplayError::contract("CONTRACT_INVALID"));
                    }
                    write_message(
                        stream,
                        &summary.to_value(&hello.client_version, &hello.schema_sha256),
                    )?;
                    return Ok(SessionEnd::Bye(summary.stderr_line()));
                }
                _ => return Err(ReplayError::contract("CONTRACT_INVALID")),
            }
        }

        Ok(SessionEnd::EofAfterHandshake(summary.stderr_line()))
    }

    fn require_kind(value: &Value, expected: &str) -> Result<(), ReplayError> {
        if value.get("kind").and_then(Value::as_str) == Some(expected) {
            Ok(())
        } else {
            Err(ReplayError::contract("CONTRACT_INVALID"))
        }
    }

    fn read_message<R: Read>(reader: &mut R) -> Result<Option<Value>, ReplayError> {
        let mut header = [0_u8; 4];
        loop {
            match reader.read(&mut header[..1]) {
                Ok(0) => return Ok(None),
                Ok(1) => break,
                Ok(_) => return Err(ReplayError::contract("CONTRACT_INVALID")),
                Err(error) if error.kind() == io::ErrorKind::Interrupted => continue,
                Err(_) => return Err(ReplayError::contract("IO_ERROR")),
            }
        }
        read_exact_framed(reader, &mut header[1..])?;
        let length = u32::from_be_bytes(header) as usize;
        if length == 0 || length > MAX_IPC_MESSAGE {
            return Err(ReplayError::contract("CONTRACT_INVALID"));
        }

        let mut bytes = vec![0_u8; length];
        read_exact_framed(reader, &mut bytes)?;
        let text =
            String::from_utf8(bytes).map_err(|_| ReplayError::contract("CONTRACT_INVALID"))?;
        let value = serde_json::from_str::<Value>(&text)
            .map_err(|_| ReplayError::contract("CONTRACT_INVALID"))?;
        if !value.is_object() {
            return Err(ReplayError::contract("CONTRACT_INVALID"));
        }
        Ok(Some(value))
    }

    fn read_exact_framed<R: Read>(reader: &mut R, bytes: &mut [u8]) -> Result<(), ReplayError> {
        match reader.read_exact(bytes) {
            Ok(()) => Ok(()),
            Err(error) if error.kind() == io::ErrorKind::UnexpectedEof => {
                Err(ReplayError::contract("CONTRACT_INVALID"))
            }
            Err(_) => Err(ReplayError::contract("IO_ERROR")),
        }
    }

    fn write_message<W: Write>(writer: &mut W, value: &Value) -> Result<(), ReplayError> {
        let bytes = serde_json::to_vec(value).map_err(|_| ReplayError::internal())?;
        write_message_bytes(writer, &bytes)
    }

    fn write_message_bytes<W: Write>(writer: &mut W, bytes: &[u8]) -> Result<(), ReplayError> {
        if bytes.is_empty() || bytes.len() > MAX_IPC_MESSAGE {
            return Err(ReplayError::internal());
        }
        let length = u32::try_from(bytes.len()).map_err(|_| ReplayError::internal())?;
        writer
            .write_all(&length.to_be_bytes())
            .and_then(|()| writer.write_all(bytes))
            .map_err(|_| ReplayError::contract("IO_ERROR"))
    }

    pub(super) fn send_error(stream: &mut UnixStream, error: &ReplayError) {
        let bytes = error_line(error.code, false);
        let _ = write_message_bytes(stream, &bytes);
    }
}
