#include <iostream>
#include <vector>
#include <unistd.h>
#include "lz4.h"


int main(int argc, char** argv)
{
   unsigned char* in = new unsigned char[10000000];
   int inSize = read(0, in, 10000000);

   const auto outMaxSize = LZ4_compressBound(inSize);
   std::vector<char> out(outMaxSize + 4);
   int result = LZ4_compress_default((char*)in, out.data() + 4, inSize, outMaxSize);
   if (result <= 0)
   {
     return 1;
   }

   // write the original size in here
   out[0] = (char)(inSize & 0xff);
   out[1] = (char)((inSize >> 8) & 0xff);
   out[2] = (char)((inSize >> 16) & 0xff);
   out[3] = (char)((inSize >> 24) & 0xff);

   out.resize(result + 4);
   write(1, out.data(), out.size());
   return 0;
}
