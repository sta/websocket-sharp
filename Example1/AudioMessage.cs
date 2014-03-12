using System;

namespace Example1
{
  internal class AudioMessage
  {
    public uint      user_id;
    public byte      ch_num;
    public uint      buffer_length;
    public float [,] buffer_array;
  }
}
