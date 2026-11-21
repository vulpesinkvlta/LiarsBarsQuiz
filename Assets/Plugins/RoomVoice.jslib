mergeInto(LibraryManager.library, {
  $BarCapture: { generation: 0, stream: null, context: null, node: null },
  BarMicStart__deps: ['$BarCapture'],
  BarMicStart: function(ptr) {
    var target = UTF8ToString(ptr), state = BarCapture, generation = ++state.generation;
    function fail() {
      if (generation === state.generation) SendMessage(target, 'MicFailed', '');
    }
    try {
      var context = new (window.AudioContext || window.webkitAudioContext)();
      state.context = context;
      Promise.all([context.resume(), navigator.mediaDevices.getUserMedia({audio: true}).then(function(stream) {
        if (generation !== state.generation) {stream.getTracks().forEach(function(t){t.stop();}); return;}
        state.stream = stream;
      })]).then(function() {
        if (generation !== state.generation) return;
        var input = context.createMediaStreamSource(state.stream);
        var node = context.createScriptProcessor(2048,1,1);
        state.node = node;
        var phase = 0, sum = 0, count = 0, bytes = '';
        node.onaudioprocess = function(e) {
          var data = e.inputBuffer.getChannelData(0);
          for (var i=0;i<data.length;i++) {
            phase += 8000; sum += data[i]; count++;
            if (phase < context.sampleRate) continue;
            phase -= context.sampleRate;
            bytes += String.fromCharCode(Math.max(0,Math.min(255,128+Math.round(127*sum/count))));
            sum=0;count=0;
            if (bytes.length === 800) {SendMessage(target,'WebVoice',btoa(bytes)); bytes='';}
          }
        };
        input.connect(node);node.connect(context.destination);
        SendMessage(target,'MicReady','');
      }).catch(fail);
    } catch(e) {fail();}
  },
  BarMicStop__deps: ['$BarCapture'],
  BarMicStop: function() {
    var s=BarCapture; s.generation++;
    if(s.node){s.node.onaudioprocess=null;s.node.disconnect();s.node=null;}
    if(s.stream){s.stream.getTracks().forEach(function(t){t.stop();});s.stream=null;}
    if(s.context){s.context.close();s.context=null;}
  }
});
