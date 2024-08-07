using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Plugin.Services;
using Dalamud.Utility.Signatures;
using ECommons.ChatMethods;
using FFXIVClientStructs.FFXIV.Client.UI;
using System;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Channels;

namespace SomethingNeedDoing.Managers;

internal class ChatManager : IDisposable
{
    private readonly Channel<string> chatBoxMessages = Channel.CreateUnbounded<string>();

    [Signature("48 89 5C 24 ?? 57 48 83 EC 20 48 8B FA 48 8B D9 45 84 C9")]
    private readonly ProcessChatBoxDelegate processChatBox = null!;

    public ChatManager()
    {
        Svc.Hook.InitializeFromAttributes(this);
        Svc.Framework.Update += FrameworkUpdate;
    }

    private unsafe delegate void ProcessChatBoxDelegate(UIModule* uiModule, IntPtr message, IntPtr unused, byte a4);

    public void Dispose()
    {
        Svc.Framework.Update -= FrameworkUpdate;
        chatBoxMessages.Writer.Complete();
    }

    public void PrintMessage(string message)
        => Svc.Chat.Print(new XivChatEntry()
        {
            Type = Service.Configuration.ChatType,
            Message = $"[{SomethingNeedDoingPlugin.Prefix}] {message}",
        });

    public void PrintColor(string message, UIColor color)
        => Svc.Chat.Print(new XivChatEntry()
        {
            Type = Service.Configuration.ChatType,
            Message = new SeString(
                new UIForegroundPayload((ushort)color),
                new TextPayload($"[{SomethingNeedDoingPlugin.Prefix}] {message}"),
                UIForegroundPayload.UIForegroundOff),
        });

    public void PrintError(string message)
        => Svc.Chat.Print(new XivChatEntry()
        {
            Type = Service.Configuration.ErrorChatType,
            Message = $"[{SomethingNeedDoingPlugin.Prefix}] {message}",
        });


    public async void SendMessage(string message) => await chatBoxMessages.Writer.WriteAsync(message);

    /// <summary>
    /// Clear the queue of messages to send to the chatbox.
    /// </summary>
    public void Clear()
    {
        var reader = chatBoxMessages.Reader;
        while (reader.Count > 0 && reader.TryRead(out var _))
            continue;
    }

    private void FrameworkUpdate(IFramework framework)
    {
        if (chatBoxMessages.Reader.TryRead(out var message))
        {
            SendMessageInternal(message);
        }
    }

    private unsafe void SendMessageInternal(string message)
    {
        var framework = FFXIVClientStructs.FFXIV.Client.System.Framework.Framework.Instance();
        var uiModule = framework->GetUIModule();

        using var payload = new ChatPayload(message);
        var payloadPtr = Marshal.AllocHGlobal(400);
        Marshal.StructureToPtr(payload, payloadPtr, false);

        processChatBox(uiModule, payloadPtr, IntPtr.Zero, 0);

        Marshal.FreeHGlobal(payloadPtr);
    }

    [StructLayout(LayoutKind.Explicit)]
    private readonly struct ChatPayload : IDisposable
    {
        [FieldOffset(0)]
        private readonly IntPtr textPtr;

        [FieldOffset(16)]
        private readonly ulong textLen;

        [FieldOffset(8)]
        private readonly ulong unk1;

        [FieldOffset(24)]
        private readonly ulong unk2;

        internal ChatPayload(string text)
        {
            var stringBytes = Encoding.UTF8.GetBytes(text);
            textPtr = Marshal.AllocHGlobal(stringBytes.Length + 30);

            Marshal.Copy(stringBytes, 0, textPtr, stringBytes.Length);
            Marshal.WriteByte(textPtr + stringBytes.Length, 0);

            textLen = (ulong)(stringBytes.Length + 1);

            unk1 = 64;
            unk2 = 0;
        }

        public void Dispose() => Marshal.FreeHGlobal(textPtr);
    }
}
