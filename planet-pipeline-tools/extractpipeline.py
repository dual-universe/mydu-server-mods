import json
import sys
import base64


with open(sys.argv[1], 'r') as fd:
    sin = fd.read()
j = json.loads(sin)
praw = j['pipeline']
plz = base64.b64decode(praw.encode())
with open(sys.argv[2], 'wb') as fd:
    fd.write(plz)