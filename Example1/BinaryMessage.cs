using System;
using System.Collections.Generic;
using WebSocketSharp;

namespace Example1
{
  internal class BinaryMessage
  {
    public uint UserID {
      get; set;
    }

    public byte ChannelNumber {
      get; set;
    }

    public uint BufferLength {
      get; set;
    }

    public float[,] BufferArray {
      get; set;
    }

    public static BinaryMessage Parse (byte[] data)
    {
      var id = data.SubArray (0, 4).To<uint> (ByteOrder.Big);
      var num = data.SubArray (4, 1)[0];
      var len = data.SubArray (5, 4).To<uint> (ByteOrder.Big);
      var arr = new float[num, len];

      var offset = 9;
      ((uint) num).Times (
        i =>
          len.Times (
            j => {
              arr[i, j] = data.SubArray (offset, 4).To<float> (ByteOrder.Big);
              offset += 4;
            }
          )
      );

      return new BinaryMessage {
               UserID = id,
               ChannelNumber = num,
               BufferLength = len,
               BufferArray = arr
             };
    }

    public byte[] ToArray ()
    {
      var buff = new List<byte> ();

      var id = UserID;
      var num = ChannelNumber;
      var len = BufferLength;
      var arr = BufferArray;

      buff.AddRange (id.ToByteArray (ByteOrder.Big));
      buff.Add (num);
      buff.AddRange (len.ToByteArray (ByteOrder.Big));

      ((uint) num).Times (
        i =>
          len.Times (
            j => buff.AddRange (arr[i, j].ToByteArray (ByteOrder.Big))
          )
      );

      return buff.ToArray ();
    }
  }
}
