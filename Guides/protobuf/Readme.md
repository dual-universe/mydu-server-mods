# Grpc and protobuf files: what you need to know


## Front connection

The front connection usese the "Public" service defined in "visibility.proto".

## NQPacketWrapper packetType

The packet type integer is obtained from the request name by the following code (in python):

    MESSAGE_MOD = 1000000
    def generateMessage(name: str, reply: bool):
        """generate a message number from the message name with a low risk of conflict."""
        res = zlib.adler32(name.encode("utf-8")) % MESSAGE_MOD
        if reply:
            res += MESSAGE_MOD
        return res

## Binary serialization

The binary encoding used by NQ puts the fields in the order they appear in the .def files
with no header or separator.

Following is the encoding for all primitive types:

- float,double,integers of 2 bytes or less: raw binary value
- string and byte array: size as a varuint followed by raw binary value
- array(vector): size as varuint followed by that many values of the struct or primitive
- uint32,uint64,varuint: variable size (1-10 bytes), 7 data bits per byte, high bit indicates if more data is present
- int32,int64,varint: zigzag encoded varuint (first bit is sign, then abs(value) shifted by one.
