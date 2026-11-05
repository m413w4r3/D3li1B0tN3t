#![cfg(unix)]

use std::fs;
use std::io::{Read, Write};
use std::os::unix::fs::{FileTypeExt, PermissionsExt};
use std::os::unix::net::UnixStream;
use std::path::{Path, PathBuf};
use std::process::{Child, Command, Output, Stdio};
use std::sync::atomic::{AtomicU64, Ordering};
use std::time::{Duration, Instant};

use serde_json::{json, Value};

const FINGERPRINT: &str = "0000000000000000000000000000000000000000000000000000000000000000";
static NEXT_SOCKET: AtomicU64 = AtomicU64::new(0);

struct Server {
    child: Option<Child>,
}

impl Server {
    fn wait(&mut self) -> Output {
        self.child
            .take()
            .expect("server child exists")
            .wait_with_output()
            .expect("server exits")
    }
}

impl Drop for Server {
    fn drop(&mut self) {
        if let Some(child) = &mut self.child {
            if child
                .try_wait()
                .expect("server state is readable")
                .is_none()
            {
                let _ = child.kill();
                let _ = child.wait();
            }
        }
    }
}

fn socket_path() -> PathBuf {
    let id = NEXT_SOCKET.fetch_add(1, Ordering::Relaxed);
    std::env::temp_dir().join(format!("d3b-ipc-{}-{id}.sock", std::process::id()))
}

fn spawn_server(path: &Path) -> Server {
    let child = Command::new(env!("CARGO_BIN_EXE_d3b-diagnostic"))
        .arg("serve")
        .arg("--socket")
        .arg(path)
        .stdin(Stdio::null())
        .stdout(Stdio::piped())
        .stderr(Stdio::piped())
        .spawn()
        .expect("server starts");
    let mut server = Server { child: Some(child) };
    let deadline = Instant::now() + Duration::from_secs(3);
    loop {
        if fs::symlink_metadata(path).is_ok_and(|metadata| {
            metadata.file_type().is_socket() && metadata.permissions().mode() & 0o777 == 0o600
        }) {
            return server;
        }
        if server
            .child
            .as_mut()
            .expect("server child exists")
            .try_wait()
            .expect("server state is readable")
            .is_some()
        {
            panic!("server exited before binding its socket");
        }
        assert!(Instant::now() < deadline, "server socket appears");
        std::thread::sleep(Duration::from_millis(5));
    }
}

fn schema() -> Value {
    json!({
        "kind": "schema",
        "messages": [{
            "name": "lok",
            "mapEntry": false,
            "fields": [
                {
                    "number": 1, "name": "geei", "kind": "INT64", "repeated": false,
                    "packed": false, "messageType": null, "enumType": null, "oneof": null,
                    "hasPresence": true
                },
                {
                    "number": 2, "name": "geej", "kind": "INT32", "repeated": false,
                    "packed": false, "messageType": null, "enumType": null, "oneof": null,
                    "hasPresence": true
                }
            ]
        }]
    })
}

fn hello() -> Value {
    json!({
        "kind": "hello",
        "protocolVersion": 1,
        "clientVersion": "3.7.13.17",
        "schemaSha256": FINGERPRINT
    })
}

fn frame(sequence: u64, payload_hex: &str) -> Value {
    json!({
        "kind": "frame",
        "sequence": sequence,
        "direction": "server",
        "typeUrl": "type.ankama.com/lok",
        "carriedType": "lok",
        "payloadHex": payload_hex
    })
}

fn write_message(stream: &mut UnixStream, value: &Value) {
    let bytes = serde_json::to_vec(value).expect("test JSON serializes");
    write_raw_message(stream, &bytes);
}

fn write_raw_message(stream: &mut UnixStream, bytes: &[u8]) {
    let length = u32::try_from(bytes.len()).expect("test message length fits u32");
    stream
        .write_all(&length.to_be_bytes())
        .and_then(|()| stream.write_all(bytes))
        .expect("message is written");
}

fn read_message(stream: &mut UnixStream) -> Value {
    let mut header = [0_u8; 4];
    stream
        .read_exact(&mut header)
        .expect("message header arrives");
    let length = u32::from_be_bytes(header) as usize;
    let mut bytes = vec![0_u8; length];
    stream.read_exact(&mut bytes).expect("message body arrives");
    serde_json::from_slice(&bytes).expect("message is JSON")
}

fn handshake(stream: &mut UnixStream) -> Value {
    write_message(stream, &hello());
    write_message(stream, &schema());
    read_message(stream)
}

fn expect_error(stream: &mut UnixStream, expected: &str) -> Value {
    let value = read_message(stream);
    assert_eq!(value["kind"], "error");
    assert_eq!(value["code"], expected);
    value
}

fn assert_failed_server(server: &mut Server, expected_code: &str) {
    let output = server.wait();
    assert_eq!(output.status.code(), Some(2));
    assert!(output.stdout.is_empty());
    let stderr = String::from_utf8(output.stderr).expect("stderr is UTF-8");
    assert_eq!(stderr.trim(), format!("code={expected_code}"));
}

#[test]
fn happy_path_analyzes_two_frames_and_matches_replay_statuses() {
    let path = socket_path();
    let mut server = spawn_server(&path);
    let mut stream = UnixStream::connect(&path).expect("client connects");
    let engine_hello = handshake(&mut stream);
    assert_eq!(engine_hello["kind"], "hello");
    assert_eq!(engine_hello["protocolVersion"], 1);

    let frames = [frame(3, "0801"), frame(9, "1002")];
    let mut ipc_statuses = Vec::new();
    for frame in &frames {
        write_message(&mut stream, frame);
        let analysis = read_message(&mut stream);
        assert_eq!(analysis["kind"], "analysis");
        assert_eq!(analysis["sequence"], frame["sequence"]);
        ipc_statuses.push(
            analysis["status"]
                .as_str()
                .expect("status exists")
                .to_owned(),
        );
    }
    write_message(&mut stream, &json!({"kind": "bye"}));
    let summary = read_message(&mut stream);
    assert_eq!(summary["kind"], "summary");
    assert_eq!(summary["frames"], 2);
    drop(stream);

    let output = server.wait();
    assert!(output.status.success());
    assert!(output.stdout.is_empty());
    assert!(String::from_utf8(output.stderr)
        .expect("stderr is UTF-8")
        .contains("frames=2 statuses=[VALID:2]"));

    let replay_input = format!(
        "{}\n{}\n{}\n{}\n{}\n",
        serde_json::to_string(&hello()).expect("hello serializes"),
        serde_json::to_string(&schema()).expect("schema serializes"),
        serde_json::to_string(&json!({
            "kind": "frame", "index": 3, "direction": "server",
            "typeUrl": "type.ankama.com/lok", "carriedType": "lok", "payloadHex": "0801"
        }))
        .expect("first frame serializes"),
        serde_json::to_string(&json!({
            "kind": "frame", "index": 9, "direction": "server",
            "typeUrl": "type.ankama.com/lok", "carriedType": "lok", "payloadHex": "1002"
        }))
        .expect("second frame serializes"),
        "{\"kind\":\"end\"}"
    );
    let mut replay = Command::new(env!("CARGO_BIN_EXE_d3b-diagnostic"))
        .arg("replay")
        .stdin(Stdio::piped())
        .stdout(Stdio::piped())
        .stderr(Stdio::piped())
        .spawn()
        .expect("replay starts");
    replay
        .stdin
        .take()
        .expect("replay stdin exists")
        .write_all(replay_input.as_bytes())
        .expect("replay input is written");
    let replay_output = replay.wait_with_output().expect("replay exits");
    assert!(replay_output.status.success());
    let replay_values = String::from_utf8(replay_output.stdout)
        .expect("replay output is UTF-8")
        .lines()
        .map(|line| serde_json::from_str::<Value>(line).expect("replay line is JSON"))
        .collect::<Vec<_>>();
    let replay_statuses = replay_values
        .iter()
        .filter(|value| value["kind"] == "analysis")
        .map(|value| value["status"].as_str().expect("status exists").to_owned())
        .collect::<Vec<_>>();
    assert_eq!(ipc_statuses, replay_statuses);
    let _ = fs::remove_file(path);
}

#[test]
fn frame_before_handshake_is_rejected() {
    let path = socket_path();
    let mut server = spawn_server(&path);
    let mut stream = UnixStream::connect(&path).expect("client connects");
    write_message(&mut stream, &frame(0, "0801"));
    expect_error(&mut stream, "CONTRACT_INVALID");
    drop(stream);
    assert_failed_server(&mut server, "CONTRACT_INVALID");
}

#[test]
fn out_of_order_sequence_is_rejected() {
    let path = socket_path();
    let mut server = spawn_server(&path);
    let mut stream = UnixStream::connect(&path).expect("client connects");
    assert_eq!(handshake(&mut stream)["kind"], "hello");
    write_message(&mut stream, &frame(5, "0801"));
    assert_eq!(read_message(&mut stream)["status"], "VALID");
    write_message(&mut stream, &frame(5, "1002"));
    expect_error(&mut stream, "CONTRACT_INVALID");
    drop(stream);
    assert_failed_server(&mut server, "CONTRACT_INVALID");
}

#[test]
fn oversized_and_zero_length_messages_are_rejected() {
    for header in [1_048_577_u32.to_be_bytes(), 0_u32.to_be_bytes()] {
        let path = socket_path();
        let mut server = spawn_server(&path);
        let mut stream = UnixStream::connect(&path).expect("client connects");
        stream.write_all(&header).expect("header is written");
        expect_error(&mut stream, "CONTRACT_INVALID");
        drop(stream);
        assert_failed_server(&mut server, "CONTRACT_INVALID");
    }
}

#[test]
fn invalid_json_and_utf8_are_rejected_without_echoing_payload() {
    for bytes in [b"not-json-SECRET".as_slice(), &[0xff, 0xfe]] {
        let path = socket_path();
        let mut server = spawn_server(&path);
        let mut stream = UnixStream::connect(&path).expect("client connects");
        write_raw_message(&mut stream, bytes);
        let error = expect_error(&mut stream, "CONTRACT_INVALID");
        let encoded = serde_json::to_string(&error).expect("error serializes");
        assert!(!encoded.contains("SECRET"));
        drop(stream);
        let output = server.wait();
        assert_eq!(output.status.code(), Some(2));
        assert!(output.stdout.is_empty());
        let stderr = String::from_utf8(output.stderr).expect("stderr is UTF-8");
        assert_eq!(stderr.trim(), "code=CONTRACT_INVALID");
        assert!(!stderr.contains("SECRET"));
    }
}

#[test]
fn second_concurrent_client_is_closed_without_disturbing_first() {
    let path = socket_path();
    let mut server = spawn_server(&path);
    let mut first = UnixStream::connect(&path).expect("first client connects");
    assert_eq!(handshake(&mut first)["kind"], "hello");

    if let Ok(mut second) = UnixStream::connect(&path) {
        second
            .set_read_timeout(Some(Duration::from_secs(2)))
            .expect("second client timeout is set");
        let mut byte = [0_u8; 1];
        assert_eq!(second.read(&mut byte).expect("second client closes"), 0);
    }

    write_message(&mut first, &frame(0, "0801"));
    assert_eq!(read_message(&mut first)["status"], "VALID");
    write_message(&mut first, &json!({"kind": "bye"}));
    assert_eq!(read_message(&mut first)["kind"], "summary");
    drop(first);
    assert!(server.wait().status.success());
    let _ = fs::remove_file(path);
}

#[test]
fn socket_has_mode_0600() {
    let path = socket_path();
    let mut server = spawn_server(&path);
    let mode = fs::symlink_metadata(&path)
        .expect("socket metadata exists")
        .permissions()
        .mode()
        & 0o777;
    assert_eq!(mode, 0o600);
    let stream = UnixStream::connect(&path).expect("client connects");
    drop(stream);
    assert_eq!(server.wait().status.code(), Some(2));
    let _ = fs::remove_file(path);
}

#[test]
fn eof_after_handshake_closes_cleanly_without_summary() {
    let path = socket_path();
    let mut server = spawn_server(&path);
    let mut stream = UnixStream::connect(&path).expect("client connects");
    assert_eq!(handshake(&mut stream)["kind"], "hello");
    drop(stream);
    let output = server.wait();
    assert!(output.status.success());
    assert!(output.stdout.is_empty());
    assert!(String::from_utf8(output.stderr)
        .expect("stderr is UTF-8")
        .contains("frames=0 statuses=[]"));
}

#[test]
fn second_handshake_is_rejected() {
    let path = socket_path();
    let mut server = spawn_server(&path);
    let mut stream = UnixStream::connect(&path).expect("client connects");
    assert_eq!(handshake(&mut stream)["kind"], "hello");
    write_message(&mut stream, &hello());
    expect_error(&mut stream, "CONTRACT_INVALID");
    drop(stream);
    assert_failed_server(&mut server, "CONTRACT_INVALID");
}

#[test]
fn unsupported_protocol_version_uses_stable_code() {
    let path = socket_path();
    let mut server = spawn_server(&path);
    let mut stream = UnixStream::connect(&path).expect("client connects");
    let mut invalid_hello = hello();
    invalid_hello["protocolVersion"] = json!(2);
    write_message(&mut stream, &invalid_hello);
    expect_error(&mut stream, "VERSION_UNSUPPORTED");
    drop(stream);
    assert_failed_server(&mut server, "VERSION_UNSUPPORTED");
}

#[test]
fn invalid_schema_and_fingerprint_use_stable_codes() {
    let path = socket_path();
    let mut server = spawn_server(&path);
    let mut stream = UnixStream::connect(&path).expect("client connects");
    write_message(&mut stream, &hello());
    write_message(
        &mut stream,
        &json!({"kind": "schema", "messages": [{"name": "", "mapEntry": false, "fields": []}]}),
    );
    expect_error(&mut stream, "SCHEMA_INVALID");
    drop(stream);
    assert_failed_server(&mut server, "SCHEMA_INVALID");

    let path = socket_path();
    let mut server = spawn_server(&path);
    let mut stream = UnixStream::connect(&path).expect("client connects");
    let mut invalid_hello = hello();
    invalid_hello["schemaSha256"] = json!("not-a-fingerprint");
    write_message(&mut stream, &invalid_hello);
    expect_error(&mut stream, "SCHEMA_FINGERPRINT_INVALID");
    drop(stream);
    assert_failed_server(&mut server, "SCHEMA_FINGERPRINT_INVALID");
}

#[test]
fn existing_socket_path_is_not_removed_or_overwritten() {
    let path = socket_path();
    fs::write(&path, b"keep").expect("existing path is created");
    let output = Command::new(env!("CARGO_BIN_EXE_d3b-diagnostic"))
        .arg("serve")
        .arg("--socket")
        .arg(&path)
        .stdin(Stdio::null())
        .stdout(Stdio::piped())
        .stderr(Stdio::piped())
        .output()
        .expect("server command runs");
    assert_eq!(output.status.code(), Some(2));
    assert!(output.stdout.is_empty());
    assert_eq!(fs::read(&path).expect("existing file remains"), b"keep");
    assert_eq!(
        String::from_utf8(output.stderr)
            .expect("stderr is UTF-8")
            .trim(),
        "code=SOCKET_FAILED"
    );
    fs::remove_file(path).expect("temporary file is removed");
}

#[test]
fn missing_parent_directory_is_not_created() {
    let parent = socket_path().with_extension("parent");
    let path = parent.join("diagnostic.sock");
    let output = Command::new(env!("CARGO_BIN_EXE_d3b-diagnostic"))
        .arg("serve")
        .arg("--socket")
        .arg(&path)
        .stdout(Stdio::piped())
        .stderr(Stdio::piped())
        .output()
        .expect("server command runs");
    assert_eq!(output.status.code(), Some(2));
    assert!(output.stdout.is_empty());
    assert!(!parent.exists());
    assert_eq!(
        String::from_utf8(output.stderr)
            .expect("stderr is UTF-8")
            .trim(),
        "code=SOCKET_FAILED"
    );
}

#[test]
fn eof_before_handshake_exits_two_without_protocol_output() {
    let path = socket_path();
    let mut server = spawn_server(&path);
    let stream = UnixStream::connect(&path).expect("client connects");
    drop(stream);
    let output = server.wait();
    assert_eq!(output.status.code(), Some(2));
    assert!(output.stdout.is_empty());
    assert_eq!(
        String::from_utf8(output.stderr)
            .expect("stderr is UTF-8")
            .trim(),
        "code=CONTRACT_INVALID"
    );
}

#[test]
fn missing_socket_argument_exits_two_on_stderr() {
    for arguments in [vec!["serve"], vec!["serve", "--bad", "ignored.sock"]] {
        let output = Command::new(env!("CARGO_BIN_EXE_d3b-diagnostic"))
            .args(arguments)
            .output()
            .expect("server command runs");
        assert_eq!(output.status.code(), Some(2));
        assert!(output.stdout.is_empty());
        assert_eq!(
            String::from_utf8(output.stderr)
                .expect("stderr is UTF-8")
                .trim(),
            "code=CONTRACT_INVALID"
        );
    }
}
