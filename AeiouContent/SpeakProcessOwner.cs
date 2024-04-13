using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Threading;

namespace AeiouContent;

sealed class SpeakProcessOwner
{
    private readonly struct State
    {
        internal readonly Process Process;
        internal readonly AnonymousPipeServerStream OutPipe;
        internal readonly AnonymousPipeServerStream InPipe;
        internal readonly BinaryReader BinaryReader;
        internal readonly StreamWriter StreamWriter;

        public State(
            Process process,
            AnonymousPipeServerStream outPipe,
            AnonymousPipeServerStream inPipe,
            BinaryReader binaryReader,
            StreamWriter streamWriter)
        {
            Process = process;
            OutPipe = outPipe;
            InPipe = inPipe;
            BinaryReader = binaryReader;
            StreamWriter = streamWriter;
        }
    }

    private readonly struct OutgoingMsg
    {
        public readonly string Message;
        public readonly Player? Player;

        public OutgoingMsg(string message, Player? player)
        {
            Message = message;
            Player = player;
        }
    }

    public readonly struct IncomingAudio
    {
        public readonly string MessageText;
        public readonly byte[] Data;
        public readonly Player? Player;

        public IncomingAudio(string messageText, byte[] data, Player? player)
        {
            MessageText = messageText;
            Data = data;
            Player = player;
        }
    }

    private readonly ConcurrentQueue<OutgoingMsg> _outgoingMsgs = new();
    private readonly object _mutex = new();
    private State? _currentState = null;

    public readonly ConcurrentQueue<IncomingAudio> IncomingAudioData = new();

    public void Init()
    {
        lock (_mutex)
        {
            if (_currentState is { } oldState)
            {
                oldState.Process.Kill();
            }

            var assemblyDir = Path.GetDirectoryName(Path.GetFullPath(typeof(Plugin).Assembly.Location));
            if (string.IsNullOrWhiteSpace(assemblyDir) || !Directory.Exists(assemblyDir))
            {
                Plugin.Instance.SelfLogger.LogError($"Failed to start SpeakServer: \"{assemblyDir}\" is not a valid directory");
                return;
            }
            var outPipe = new AnonymousPipeServerStream(PipeDirection.Out, HandleInheritability.Inheritable);
            var inPipe = new AnonymousPipeServerStream(PipeDirection.In, HandleInheritability.Inheritable);

            var processInfo = new ProcessStartInfo
            {
                FileName = Path.Combine(assemblyDir, "./SpeakServer.exe"),
                UseShellExecute = false
            };
            processInfo.Arguments = outPipe.GetClientHandleAsString() + " " + inPipe.GetClientHandleAsString();
            var process = Process.Start(processInfo);
            if (process is null)
            {
                Plugin.Instance.SelfLogger.LogError($"Failed to start SpeakServer: program could not launch");
                return;
            }
            var newStreamWriter = new StreamWriter(outPipe);
            var newBinaryReader = new BinaryReader(inPipe);
            _currentState = new State(
                process: process,
                outPipe: outPipe,
                inPipe: inPipe,
                binaryReader: newBinaryReader,
                streamWriter: newStreamWriter);
        }
        new Thread(ServerCommunicationLoop).Start();
        Plugin.Instance.SelfLogger.LogInfo("Starting up AeiouContent!");
    }

    private State? GetState()
    {
        lock (_mutex)
        {
            return _currentState;
        }
    }

    public void Speak(string message, Player? player)
    {
        _outgoingMsgs.Enqueue(new OutgoingMsg(message: message, player: player));
    }

    private void ServerCommunicationLoop()
    {
        if (GetState() is not { } selfState)
        {
            return;
        }
        while (true)
        {
            if (!selfState.OutPipe.IsConnected
                || !selfState.InPipe.IsConnected
                ||GetState() is not { } currentState
                || !currentState.Equals(selfState))
            {
                break;
            }

            try
            {
                while (_outgoingMsgs.TryDequeue(out var msg))
                {
                    selfState.StreamWriter.WriteLine($"msg={msg.Message}");
                    selfState.StreamWriter.Flush();
                    var audioByteCount = selfState.BinaryReader.ReadInt32();
                    if (audioByteCount <= 0) { continue; }
                    var audioBytes = selfState.BinaryReader.ReadBytes(audioByteCount);
                    IncomingAudioData.Enqueue(new IncomingAudio(messageText: msg.Message, data: audioBytes, player: msg.Player));
                }
            }
            catch (Exception exception)
            {
                Plugin.Instance.SelfLogger.LogError($"Exception in ServerCommunicationLoop: {exception}");
            }
            Thread.Sleep(32);
        }
    }
}