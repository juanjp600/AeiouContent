using System;
using System.IO;
using System.IO.Pipes;
using System.Text;
using SharpTalk;

namespace SpeakServer
{
    static class Program
    {
        const string MessagePrefix = "msg=";

        static void Main(string[] args)
        {
            if (args.Length < 2)
            {
                Console.WriteLine(value: $"AeiouCompany SpeakServer: 2 pipes required, got {args.Length}, closing");
                return;
            }

            using var inPipe = new AnonymousPipeClientStream(
                direction: PipeDirection.In,
                pipeHandleAsString: args[0]);
            using var outPipe = new AnonymousPipeClientStream(
                direction: PipeDirection.Out,
                pipeHandleAsString: args[1]);
            using var streamReader = new StreamReader(
                stream: inPipe,
                encoding: Encoding.UTF8,
                detectEncodingFromByteOrderMarks: true,
                bufferSize: 8192,
                leaveOpen: true);
            using var binaryWriter = new BinaryWriter(output: outPipe, encoding: Encoding.UTF8, leaveOpen: true);
            var tts = new FonixTalkEngine();

            Console.WriteLine(value: "AeiouCompany SpeakServer: Connected!");

            ListenForMessages(outPipe, streamReader, binaryWriter, tts);

            tts.Sync();
        }
        static void ListenForMessages(AnonymousPipeClientStream pipeClientStream, StreamReader streamReader, BinaryWriter binaryWriter, FonixTalkEngine tts)
        {
            while (true)
            {
                if (!pipeClientStream.IsConnected)
                {
                    break;
                }
                try
                {
                    var line = streamReader.ReadLine() ?? "";
                    Console.WriteLine($"AeiouCompany SpeakServer: Incoming line \"{line}\"");
                    if (string.IsNullOrWhiteSpace(line))
                    {
                        binaryWriter.Write(value: (int)0);
                        binaryWriter.Flush();
                        continue;
                    }
                    if (line == "exit")
                    {
                        return;
                    }
                    if (line.StartsWith(value: MessagePrefix, comparisonType: StringComparison.Ordinal))
                    {
                        var message = line.Substring(startIndex: MessagePrefix.Length);
                        var buffer = tts.SpeakToMemory(input: $"[:np]{message}]");
                        binaryWriter.Write(value: (int)buffer.Length);
                        binaryWriter.Write(buffer: buffer);
                        binaryWriter.Flush();
                        Console.WriteLine($"AeiouCompany SpeakServer: Output {buffer.Length} bytes of audio");
                    }
                }
                catch (IOException e)
                {
                    Console.WriteLine(value: $"Broken: {e.Message}");
                    break;
                }
            }
        }
    }
}

