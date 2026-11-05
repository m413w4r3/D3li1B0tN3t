use std::io::{self, Write};

use d3b_diagnostic::error::ReplayError;
use d3b_diagnostic::replay::{replay, write_error, write_output};

fn main() {
    let arguments = std::env::args().skip(1).collect::<Vec<_>>();
    let pretty = match arguments.as_slice() {
        [command] if command == "replay" => false,
        [command, option] if command == "replay" && option == "--pretty" => true,
        _ => {
            report_error(ReplayError::contract("UNKNOWN_COMMAND"), false);
            std::process::exit(2);
        }
    };

    let stdin = io::stdin();
    let mut input = stdin.lock();
    match replay(&mut input, pretty) {
        Ok(output) => {
            let stdout = io::stdout();
            let mut out = stdout.lock();
            if write_output(&mut out, &output).is_err() {
                eprintln!("code=INTERNAL_ERROR");
                std::process::exit(3);
            }
            let _ = writeln!(io::stderr().lock(), "{}", output.stderr_summary);
        }
        Err(error) => {
            let exit_code = error.exit_code();
            report_error(error, pretty);
            std::process::exit(exit_code);
        }
    }
}

fn report_error(error: ReplayError, pretty: bool) {
    let stdout = io::stdout();
    let mut out = stdout.lock();
    let _ = write_error(&mut out, error.code, pretty);
    eprintln!("code={}", error.code);
}
