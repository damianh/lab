using System;

namespace Consumer
{
    class Program
    {
        static void Main(string[] args)
        {
            HelloWorldGenerated.HelloWorld.SayHello(s => Console.WriteLine(s));
        }
    }
}
