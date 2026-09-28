"""Report exported choices without a decoded reply or linked next menu."""

from html.parser import HTMLParser
from pathlib import Path
import sys


class ChoiceParser(HTMLParser):
    def __init__(self):
        super().__init__(convert_charrefs=True)
        self.details = []
        self.choices = []
        self.capture = False

    def current(self):
        return next((item for item in reversed(self.details) if item and item.get("choice") is not None), None)

    def handle_starttag(self, tag, attrs):
        attrs = dict(attrs)
        classes = attrs.get("class", "").split()
        if tag == "details":
            self.details.append({"choice": "", "no_reply": False, "no_link": False} if "option" in classes else None)
        elif tag == "span" and "choice" in classes and self.current() is not None:
            self.capture = True

    def handle_endtag(self, tag):
        if tag == "span":
            self.capture = False
        elif tag == "details" and self.details:
            item = self.details.pop()
            if item is not None:
                self.choices.append(item)

    def handle_data(self, data):
        current = self.current()
        if current is None:
            return
        if self.capture:
            current["choice"] += data
        if "No literal spoken reply was decoded" in data:
            current["no_reply"] = True
        if "No later choice menu was linked" in data:
            current["no_link"] = True


path = Path(sys.argv[1] if len(sys.argv) > 1 else "Games/indy3/indy3-conversations.html")
parser = ChoiceParser()
parser.feed(path.read_text(encoding="utf-8"))
silent = [item for item in parser.choices if item["no_reply"] and item["no_link"]]
print(f"choices={len(parser.choices)} no_reply_and_no_link={len(silent)}")
for item in silent:
    print(item["choice"])
