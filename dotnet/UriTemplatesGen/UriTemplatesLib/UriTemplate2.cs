using System;

namespace UriTemplatesLib
{
    public class UriTemplate2
    {
        public UriTemplate2(string template, Action<string> writeLine)
        {
            HelloWorldGenerated.HelloWorld.SayHello(writeLine);
        }
    }
}
