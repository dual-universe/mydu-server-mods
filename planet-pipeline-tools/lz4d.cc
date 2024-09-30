#include <iostream>
#include <unistd.h>
#include <fcntl.h>
#include "lz4.h"


int main(int argc, char** argv)
{
   int fd = 0;
   if (argc > 1)
     fd = open(argv[1], O_RDONLY 
#ifdef __MINGW32__
| O_BINARY
#endif
);
   unsigned char* in = new unsigned char[1000000];
   int count = 0;
   while (true) {
     int nb = read(fd, in+count, 1000000);
     if (nb <= 0)
       break;
     count += nb;
   }
   const int outSize = (unsigned char)in[0] | ((unsigned char)in[1] << 8) | ((unsigned char)in[2] << 16) |
                            ((unsigned char)in[3] << 24);

        std::string res;
        res.resize(outSize);

        int result = LZ4_decompress_safe(((char*)in) + 4, (char *)res.data(), count - 4, outSize);
        if (result < 0)
        {
            return 1;
        }

        res.resize(result);
        std::cout << res << std::endl;
        return 0;

}
