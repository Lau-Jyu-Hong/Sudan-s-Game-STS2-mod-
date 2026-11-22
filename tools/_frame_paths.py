from pathlib import Path

data = Path(r"F:\SteamLibrary\steamapps\workshop\content\2868840\3737335127\BaseLib\BaseLib.dll").read_bytes()
for needle in (b"Frame", b"PortraitBorder", b"BannerTexture"):
    print("\n==", needle)
    start = 0
    shown = 0
    while shown < 12:
        index = data.find(needle, start)
        if index < 0:
            break
        chunk = data[max(0, index - 30): index + 50]
        text = "".join(chr(b) if 32 <= b < 127 else "." for b in chunk)
        if "get_" in text or "Path" in text or "Frame" in text:
            print(text)
            shown += 1
        start = index + len(needle)
