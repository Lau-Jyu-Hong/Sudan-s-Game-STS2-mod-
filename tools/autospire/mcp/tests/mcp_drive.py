"""autoSpire MCP 测试驱动。

两种用法：
  1. 交互模式：python mcp/tests/mcp_drive.py
     从 stdin 逐行读 JSON：{"method":"tools/call","params":{"name":"get_state","arguments":{}}}
     每行打印一行响应 JSON。

  2. 脚本模式：python mcp/tests/mcp_drive.py --script flow.json
     flow.json 为数组，每项：
       {"method": "tools/call", "params": {...}, "expect": "子串", "expect_any": ["a","b"], "sleep": 秒}
     输出每步 PASS/FAIL 与最终汇总。expect 匹配响应 JSON 序列化文本。

驱动进程会启动 mcp/server.py 子进程，走真实 MCP stdio 协议。
"""

import argparse
import json
import subprocess
import sys
import time
from pathlib import Path

PROJECT_ROOT = Path(__file__).resolve().parents[2]


class MCPClient:
    def __init__(self):
        self._proc = subprocess.Popen(
            [sys.executable, "mcp/server.py"],
            cwd=PROJECT_ROOT,
            stdin=subprocess.PIPE,
            stdout=subprocess.PIPE,
            text=True,
            encoding="utf-8",
            errors="replace",
            bufsize=1,
        )
        self._rid = 0

    def rpc(self, method, params=None):
        self._rid += 1
        msg = {"jsonrpc": "2.0", "id": self._rid, "method": method}
        if params is not None:
            msg["params"] = params
        self._proc.stdin.write(json.dumps(msg, ensure_ascii=False) + "\n")
        self._proc.stdin.flush()
        while True:
            line = self._proc.stdout.readline()
            if not line:
                return None
            try:
                obj = json.loads(line)
            except Exception:
                continue
            if obj.get("id") == self._rid:
                return obj

    def notify(self, method, params=None):
        msg = {"jsonrpc": "2.0", "method": method}
        if params is not None:
            msg["params"] = params
        self._proc.stdin.write(json.dumps(msg, ensure_ascii=False) + "\n")
        self._proc.stdin.flush()

    def start(self):
        init = self.rpc(
            "initialize",
            {
                "protocolVersion": "2024-11-05",
                "capabilities": {},
                "clientInfo": {"name": "autospire-test", "version": "1.0"},
            },
        )
        self.notify("notifications/initialized")
        return init is not None

    def call(self, name, arguments):
        resp = self.rpc(
            "tools/call",
            {"name": name, "arguments": arguments or {}},
        )
        return resp

    def close(self):
        try:
            self._proc.kill()
        except Exception:
            pass


def flatten(resp):
    """取 tools/call 返回的纯文本（即游戏 JSON），失败时退回整个响应 JSON。"""
    try:
        text = resp["result"]["content"][0]["text"]
        if isinstance(text, str):
            return text
    except Exception:
        pass
    return json.dumps(resp, ensure_ascii=False)

def run_script(client, steps, offset=0):
    passed = 0
    failed = 0
    for idx, step in enumerate(steps, 1):
        real_idx = offset + idx
        if "sleep" in step:
            time.sleep(float(step["sleep"]))
            print(f"[{real_idx:03d}] SLEEP {step['sleep']}s")
            continue
        method = step.get("method", "tools/call")
        params = step.get("params")
        resp = client.rpc(method, params)
        text = flatten(resp)
        expect = step.get("expect")
        expect_any = step.get("expect_any")
        ok = True
        reason = ""
        if expect is not None:
            ok = expect in text
            reason = f"expect {expect!r}"
        elif expect_any:
            ok = any(e in text for e in expect_any)
            reason = f"expect_any {expect_any!r}"
        if ok:
            passed += 1
            print(f"[{real_idx:03d}] PASS  {reason or method}  {text[:220]}")
        else:
            failed += 1
            print(f"[{real_idx:03d}] FAIL  {reason or method}  {text[:220]}")
        if step.get("if_any") and ok:
            sub_ok = run_script(client, step["then"], offset=real_idx * 100)
            if not sub_ok:
                failed += 1
        elif step.get("if_any"):
            print(f"[{real_idx:03d}] SKIP  条件分支（未匹配 {step['if_any']}）")
    print(f"--- 子流程 {offset}: {passed} passed, {failed} failed ---")
    return failed == 0


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--script", help="JSON 脚本文件路径")
    args = ap.parse_args()

    client = MCPClient()
    if not client.start():
        print("MCP initialize failed")
        client.close()
        return 1
    print("MCP connected")

    if args.script:
        with open(args.script, "r", encoding="utf-8") as f:
            steps = json.load(f)
        ok = run_script(client, steps, offset=0)
        client.close()
        return 0 if ok else 1

    for raw in sys.stdin:
        raw = raw.strip()
        if not raw:
            continue
        try:
            req = json.loads(raw)
        except Exception as exc:
            print(json.dumps({"error": f"bad request: {exc}"}, ensure_ascii=False))
            continue
        method = req.get("method")
        params = req.get("params")
        if method == "notifications/initialized":
            client.notify(method)
            continue
        resp = client.rpc(method, params)
        print(flatten(resp), flush=True)

    client.close()
    return 0


if __name__ == "__main__":
    sys.exit(main())
