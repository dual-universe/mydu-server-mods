#! /usr/bin/env python3

import sys
import json

new_seed = int(sys.argv[3])
if sys.argv[1] == '-':
    sin = sys.stdin.read()
else:
    with open(sys.argv[1], 'r') as fd:
        sin = fd.read()

jin = json.loads(sin)

for n in jin['nodes']:
    if n['moduleName'] == 'VoxelCarving':
        print('found carving')
        for p in n['parameters']['data']:
            for lel in p:
                try:
                    print('found list')
                    for ll in lel:
                        for e in ll['elements']:
                            #print(e)
                            e['seed'] = new_seed
                            print('seed replaced')
                        print(ll['seed'])
                        ll['seed'] = new_seed
                except Exception as e:
                    print(e)
                    pass


sout = json.dumps(jin)
if sys.argv[2] == '-':
    sys.stdout.write(sout)
else:
    with open(sys.argv[2], 'w') as fd:
        fd.write(sout)
