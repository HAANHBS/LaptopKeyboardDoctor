#!/usr/bin/env python3
"""Portable package checks runnable before the Windows-native build."""

from __future__ import annotations

import pathlib
import re
import sys
import xml.etree.ElementTree as ET


ROOT = pathlib.Path(__file__).resolve().parents[1]
SRC = ROOT / "src"


def fail(message: str) -> None:
    raise AssertionError(message)


def strip_csharp_literals(text: str) -> str:
    """Remove comments and string/char contents before delimiter balancing."""
    output: list[str] = []
    i = 0
    state = "code"
    while i < len(text):
        ch = text[i]
        nxt = text[i + 1] if i + 1 < len(text) else ""
        if state == "code":
            if ch == "/" and nxt == "/":
                state = "line_comment"
                output.extend("  ")
                i += 2
                continue
            if ch == "/" and nxt == "*":
                state = "block_comment"
                output.extend("  ")
                i += 2
                continue
            if ch == "@" and nxt == '"':
                state = "verbatim_string"
                output.extend("  ")
                i += 2
                continue
            if ch == '"':
                state = "string"
                output.append(" ")
                i += 1
                continue
            if ch == "'":
                state = "char"
                output.append(" ")
                i += 1
                continue
            output.append(ch)
            i += 1
            continue
        if state == "line_comment":
            if ch in "\r\n":
                state = "code"
                output.append(ch)
            else:
                output.append(" ")
            i += 1
            continue
        if state == "block_comment":
            if ch == "*" and nxt == "/":
                state = "code"
                output.extend("  ")
                i += 2
            else:
                output.append("\n" if ch == "\n" else " ")
                i += 1
            continue
        if state == "string":
            if ch == "\\":
                output.extend("  " if nxt else " ")
                i += 2 if nxt else 1
            elif ch == '"':
                state = "code"
                output.append(" ")
                i += 1
            else:
                output.append("\n" if ch == "\n" else " ")
                i += 1
            continue
        if state == "verbatim_string":
            if ch == '"' and nxt == '"':
                output.extend("  ")
                i += 2
            elif ch == '"':
                state = "code"
                output.append(" ")
                i += 1
            else:
                output.append("\n" if ch == "\n" else " ")
                i += 1
            continue
        if state == "char":
            if ch == "\\":
                output.extend("  " if nxt else " ")
                i += 2 if nxt else 1
            elif ch == "'":
                state = "code"
                output.append(" ")
                i += 1
            else:
                output.append(" ")
                i += 1
    if state not in {"code", "line_comment"}:
        fail(f"unterminated C# token state: {state}")
    return "".join(output)


def check_balanced(text: str, filename: str) -> None:
    cleaned = strip_csharp_literals(text)
    pairs = {"}": "{", ")": "(", "]": "["}
    stack: list[tuple[str, int]] = []
    for index, char in enumerate(cleaned):
        if char in "{([":
            stack.append((char, index))
        elif char in "})]":
            if not stack or stack[-1][0] != pairs[char]:
                fail(f"unbalanced {char} in {filename} at offset {index}")
            stack.pop()
    if stack:
        fail(f"unclosed {stack[-1][0]} in {filename} at offset {stack[-1][1]}")


def require(text: str, token: str, filename: str) -> None:
    if token not in text:
        fail(f"missing {token!r} in {filename}")


def main() -> int:
    sources = sorted(SRC.glob("*.cs"))
    if len(sources) < 10:
        fail(f"expected at least 10 C# source files, found {len(sources)}")

    all_source = "\n".join(path.read_text(encoding="utf-8") for path in sources)
    for path in sources:
        check_balanced(path.read_text(encoding="utf-8"), path.name)

    contracts = [
        "RegisterRawInputDevices",
        "GetRawInputData",
        "GetRawInputDeviceInfo",
        "SetWindowsHookEx",
        "GetAsyncKeyState",
        "RIDEV_INPUTSINK",
        "RIDEV_DEVNOTIFY",
        "LLKHF_INJECTED",
        "PHANTOM_KEY",
        "IDLE_CONTACT",
        "CHATTER",
        "STUCK_KEY",
        "RAW_MISSING_KEYUP",
        "REPEAT_STORM",
        "RawMouse",
        "RAWMOUSE",
        "RI_MOUSE_LEFT_BUTTON_DOWN",
        "RI_MOUSE_RIGHT_BUTTON_DOWN",
        "TOUCHPAD_CHATTER",
        "TOUCHPAD_STUCK",
        "NON_HUMAN_PULSE",
        "MACHINE_RHYTHM",
        "HUMAN_TIMING_OUTLIER",
        "SystemParametersInfo",
        "ProcessCmdKey",
        "RESET / ĐO LẠI",
        "CallNextHookEx",
        "--self-test",
    ]
    for token in contracts:
        require(all_source, token, "C# sources")

    hook_match = re.search(
        r"private IntPtr HookCallback\(.*?\n\s*}\n\s*public void Dispose",
        all_source,
        flags=re.DOTALL,
    )
    if not hook_match:
        fail("cannot locate low-level hook callback")
    hook_body = hook_match.group(0)
    require(hook_body, "CallNextHookEx", "HookCallback")
    if re.search(r"return\s+(?:new\s+IntPtr\s*\(\s*1\s*\)|\(IntPtr\)\s*1)\s*;", hook_body):
        fail("hook must observe, not suppress keyboard input")

    ET.parse(ROOT / "app.manifest")
    if (ROOT / "LaptopKeyboardDoctor.exe.config").exists():
        fail("external .exe.config is forbidden by the one-file USB requirement")

    license_text = (ROOT / "LICENSE").read_text(encoding="utf-8")
    require(license_text, "MIT License", "LICENSE")
    require(license_text, "MÁY TÍNH HÀ ANH", "LICENSE")

    assembly = (SRC / "AssemblyInfo.cs").read_text(encoding="utf-8")
    for token in [
        'AssemblyCompany("MÁY TÍNH HÀ ANH")',
        'AssemblyFileVersion("0.3.0.0")',
        'AssemblyInformationalVersion("0.3.0")',
        "MÁY TÍNH HÀ ANH",
    ]:
        require(assembly, token, "AssemblyInfo.cs")

    main_form = (SRC / "MainForm.cs").read_text(encoding="utf-8")
    for token in [
        "BuildAboutTab",
        "xã Như Thanh, tỉnh Thanh Hoá",
        "System.Drawing.Icon.ExtractAssociatedIcon",
        "https://github.com/HAANHBS/LaptopKeyboardDoctor",
    ]:
        require(main_form, token, "MainForm.cs")

    build = (ROOT / "Build.ps1").read_text(encoding="utf-8")
    try:
        build.encode("ascii")
    except UnicodeEncodeError as exc:
        fail(f"Build.ps1 must remain ASCII for Windows PowerShell 5.1: {exc}")
    for token in [
        "Set-StrictMode",
        "csc.exe",
        "/target:winexe",
        "/warn:4",
        "/codepage:65001",
        "/win32icon:$iconPath",
        "SELF TEST: PASS",
        "GetTempPath",
        "Get-FileHash",
        "SINGLE-FILE DIST: PASS",
        "dist must contain only LaptopKeyboardDoctor.exe",
    ]:
        require(build, token, "Build.ps1")
    if "Copy-Item -LiteralPath $configPath" in build:
        fail("Build.ps1 must not copy an external EXE config")

    icon_source = ROOT / "assets" / "app-icon.svg"
    ET.parse(icon_source)

    workflow = (ROOT / ".github" / "workflows" / "build-windows.yml").read_text(encoding="utf-8")
    for token in [
        "windows-latest",
        "Build.ps1",
        "LaptopKeyboardDoctor.exe",
        "actions/upload-artifact@v4",
        "gh release create",
    ]:
        require(workflow, token, "build-windows.yml")

    release_version = (ROOT / ".release" / "version").read_text(encoding="utf-8").strip()
    if not re.fullmatch(r"v\d+\.\d+\.\d+", release_version):
        fail(f"invalid .release/version: {release_version!r}")
    release_workflow = (ROOT / ".github" / "workflows" / "release-windows.yml").read_text(encoding="utf-8")
    for token in [
        ".release/version",
        "windows-latest",
        "Build.ps1",
        "exactly one LaptopKeyboardDoctor.exe",
        "gh release create",
        "--target $env:GITHUB_SHA",
    ]:
        require(release_workflow, token, "release-windows.yml")

    run_cmd = (ROOT / "RUN_LaptopKeyboardDoctor.cmd").read_text(encoding="utf-8")
    require(run_cmd, "Build.ps1", "RUN_LaptopKeyboardDoctor.cmd")

    forbidden = ["RIDEV_NOLEGACY", "BlockInput(", "return (IntPtr)1;"]
    for token in forbidden:
        if token in all_source:
            fail(f"unsafe/unwanted behavior present: {token}")

    print("PACKAGE STRUCTURE: PASS")
    print("C# DELIMITER/LITERAL SCAN: PASS")
    print("WINDOWS INPUT CONTRACTS: PASS")
    print("NON-BLOCKING HOOK CONTRACT: PASS")
    print("XML MANIFEST/ICON: PASS")
    print("BUILD/SELF-TEST CONTRACT: PASS")
    print("OPEN-SOURCE/AUTHOR CONTRACT: PASS")
    print("ONE-FILE USB CONTRACT: PASS")
    print("GITHUB WINDOWS WORKFLOW: PASS")
    print("GITHUB RELEASE WORKFLOW: PASS")
    print("WINDOWS POWERSHELL 5.1 ENCODING: PASS")
    print("TOUCHPAD/POINTER CONTRACTS: PASS")
    print("HUMAN TIMING MODEL CONTRACTS: PASS")
    print("ARROW TAB-NAVIGATION GUARD: PASS")
    print(f"C# files: {len(sources)}")
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except Exception as exc:
        print(f"VERIFY: FAIL: {exc}", file=sys.stderr)
        raise
