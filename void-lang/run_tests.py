#!/usr/bin/env python3
"""Простые тесты VOID — проверяем что интерпретатор работает"""
import subprocess, sys, os

HERE = os.path.dirname(__file__)
VOID = os.path.join(HERE, "void.py")

def run_void(code):
    # записать временный файл
    import tempfile
    with tempfile.NamedTemporaryFile(mode="w", suffix=".void", delete=False, encoding="utf-8") as f:
        f.write(code)
        fname = f.name
    try:
        r = subprocess.run([sys.executable, VOID, fname], capture_output=True, text=True, encoding="utf-8")
        return r.stdout, r.stderr, r.returncode
    finally:
        os.unlink(fname)

def test(name, code, should_contain):
    out, err, code_ret = run_void(code)
    ok = should_contain in out and code_ret==0
    status = "✓" if ok else "✗"
    print(f"{status} {name}: {'PASS' if ok else 'FAIL'}")
    if not ok:
        print(f"  expected to contain: {should_contain!r}")
        print(f"  got stdout: {out!r}")
        print(f"  got stderr: {err!r}")
    return ok

all_ok = True
all_ok &= test("hello", 'бездна main { эхо "привет"; }', "привет")
all_ok &= test("arith", 'бездна main { эхо 2+3; }', "5")
all_ok &= test("var", 'бездна main { связать x=10; эхо x; }', "10")
all_ok &= test("if", 'бездна main { если 5>3 { эхо "да"; } иначе { эхо "нет"; } }', "да")
all_ok &= test("for range", 'бездна main { для i в 0..3 { эхо i; } }', "2")
all_ok &= test("func", 'призвать удвоить(n){вернуть n*2;} бездна main{эхо удвоить(21);}', "42")
all_ok &= test("recurse fib", 'призвать фиб(n){если n<=1{вернуть n;} вернуть фиб(n-1)+фиб(n-2);} бездна main{эхо фиб(6);}', "8")
all_ok &= test("paradox ?!", 'бездна main{связать a= пустота ?! 99; эхо a;}', "99")
all_ok &= test("paradox collapse", 'бездна main{парадокс p => 10/0 ?! 42; коллапс p -> y; эхо y;}', "42")
all_ok &= test("english", 'abyss main { echo "hello"; }', "hello")

for ex in ["hello.void","fib.void","paradox.void","control.void","english.void","quantum.void"]:
    path = os.path.join(HERE, "examples", ex)
    r = subprocess.run([sys.executable, VOID, path], capture_output=True, text=True, encoding="utf-8")
    ok = r.returncode==0
    print(f"{'✓' if ok else '✗'} example {ex}: {'PASS' if ok else 'FAIL'}")
    if not ok:
        print(r.stdout, r.stderr)
    all_ok &= ok

sys.exit(0 if all_ok else 1)
