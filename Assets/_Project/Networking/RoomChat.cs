using System;
using System.Collections.Generic;
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

namespace LiarsBar.Networking
{
    public interface IRoomChat
    {
        event Action RoomEnded;
        event Action<string> Message;
        event Action<string> MicStatus;
        bool SendText(string value);
        void ToggleMic();
    }

    public sealed class RoomChat : MonoBehaviourPunCallbacks, IOnEventCallback, IRoomChat
    {
        private const byte TextCode = 20, VoiceCode = 21;
        private const int Rate = 8000, Frame = 800;
        public event Action RoomEnded;
        public event Action<string> Message;
        public event Action<string> MicStatus;
        private bool _mic;
        private readonly Dictionary<int,double> _ends = new Dictionary<int,double>();
        private readonly List<AudioSource> _playing = new List<AudioSource>();
#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")] private static extern void BarMicStart(string target);
        [System.Runtime.InteropServices.DllImport("__Internal")] private static extern void BarMicStop();
#else
        private AudioClip _recording;
        private int _read;
#endif
        public bool SendText(string value)
        {
            value = value?.Trim();
            return PhotonNetwork.InRoom && !string.IsNullOrEmpty(value) && value.Length <= 240 &&
                PhotonNetwork.RaiseEvent(TextCode,value,new RaiseEventOptions { Receivers = ReceiverGroup.All },SendOptions.SendReliable);
        }

        public void ToggleMic()
        {
            if (_mic) { StopMic(); return; }
            if (!PhotonNetwork.InRoom) return;
            _mic = true;
            MicStatus?.Invoke("Микрофон…");
#if UNITY_WEBGL && !UNITY_EDITOR
            BarMicStart(gameObject.name);
#else
            try
            {
                if (Microphone.devices.Length == 0) { MicFailed(""); return; }
                _recording = Microphone.Start(null,true,1,Rate);
                if (_recording == null) { MicFailed(""); return; }
                _read = 0;
                MicReady("");
            }
            catch (Exception) { MicFailed(""); }
#endif
        }

        [UnityEngine.Scripting.Preserve]
        public void MicReady(string unused) { if (_mic) MicStatus?.Invoke("Выключить микрофон"); }
        [UnityEngine.Scripting.Preserve]
        public void MicFailed(string unused) { StopMic(); MicStatus?.Invoke("Микрофон недоступен · повторить"); }
        [UnityEngine.Scripting.Preserve]
        public void WebVoice(string encoded)
        {
            if (_mic && encoded?.Length == 1068) Broadcast(Convert.FromBase64String(encoded));
        }

        private void Broadcast(byte[] frame)
        {
            if (_mic && PhotonNetwork.InRoom)
                PhotonNetwork.RaiseEvent(VoiceCode,frame,new RaiseEventOptions { Receivers = ReceiverGroup.Others },SendOptions.SendUnreliable);
        }

        private void Update()
        {
#if !UNITY_WEBGL || UNITY_EDITOR
            if (_mic && _recording != null)
            {
                int end = Microphone.GetPosition(null);
                if (end < 0) { MicFailed(""); return; }
                while ((end-_read+_recording.samples)%_recording.samples >= Frame)
                {
                    var samples = new float[Frame*_recording.channels];
                    _recording.GetData(samples,_read);
                    var bytes = new byte[Frame];
                    for(int i=0;i<Frame;i++) bytes[i]=(byte)Mathf.Clamp(Mathf.RoundToInt(samples[i*_recording.channels]*127)+128,0,255);
                    Broadcast(bytes);
                    _read=(_read+Frame)%_recording.samples;
                }
            }
#endif
        }

        public void OnEvent(EventData e)
        {
            var player = PhotonNetwork.CurrentRoom?.GetPlayer(e.Sender);
            if (player == null) return;
            if (e.Code == TextCode && e.CustomData is string text && text.Length <= 240)
                Message?.Invoke(player.NickName + ": " + text);
            if (e.Code != VoiceCode || !(e.CustomData is byte[] frame) || frame.Length != Frame) return;
            double now = AudioSettings.dspTime;
            _ends.TryGetValue(e.Sender,out double start);
            if (start > now + .45) return;
            start = Math.Max(start,now+.035);
            var source = _playing.Find(s=>!s.isPlaying);
            if (source == null) { source = gameObject.AddComponent<AudioSource>(); source.playOnAwake=false; _playing.Add(source); }
            if (source.clip != null) Destroy(source.clip);
            var clip = AudioClip.Create("Room voice",Frame,1,Rate,false);
            var pcm = new float[Frame];
            for (int i=0;i<Frame;i++) pcm[i]=(frame[i]-128)/128f;
            clip.SetData(pcm,0);
            source.clip=clip;
            source.PlayScheduled(start);
            _ends[e.Sender]=start+(double)Frame/Rate;
        }

        private void StopMic()
        {
            if (_mic)
            {
#if UNITY_WEBGL && !UNITY_EDITOR
                BarMicStop();
#else
                if (_recording != null) { Microphone.End(null); Destroy(_recording); _recording=null; }
#endif
            }
            _mic=false;
            MicStatus?.Invoke("Включить микрофон");
        }

        public void EndRoom()
        {
            StopMic();
            foreach (var source in _playing) { source.Stop(); if(source.clip!=null) Destroy(source.clip); Destroy(source); }
            _playing.Clear();
            _ends.Clear();
            RoomEnded?.Invoke();
        }

        public override void OnDisconnected(DisconnectCause cause) => EndRoom();
        public override void OnLeftRoom() => EndRoom();
        public override void OnDisable() { RoomEnded = null; MicStatus = null; EndRoom(); base.OnDisable(); }
    }
}
