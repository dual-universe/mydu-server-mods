import json
import sys
import base64


with open(sys.argv[1], 'r') as fd:
    sin = fd.read()
with open(sys.argv[2], 'rb') as fd:
    raw = fd.read()
encoded = base64.b64encode(raw)

j = json.loads(sin)
j['pipeline'] = encoded.decode('utf-8')
sout = json.dumps(j)

with open(sys.argv[3], 'w') as fd:
    fd.write(sout)
