using System;

namespace Example4
{
    public class Program
    {
        public Program()
        {
            GetString("Hi");
            GetByte("World");
        }

        private async void GetString(string msg)// or byte[] msg
        {
            string res = await ((IConnect)new Connect()).Connection("127.0.0.1", "3469").SendMessage(msg);
            Console.WriteLine(res);
        }

        private async void GetByte(string msg)// or byte[] msg
        {
            byte[] res = await ((IConnect)new Connect()).Connection("127.0.0.1", "3469").GetBytes(msg);
            Console.WriteLine(res);
        }
    }
}