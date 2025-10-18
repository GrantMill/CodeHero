window.codeheroAudio = (function(){
  let mediaRecorder;
  let chunks = [];
  let stream;
  // PCM fallback using WebAudio ScriptProcessor
  let pcmCtx = null;
  let pcmSource = null;
  let pcmProcessor = null;
  let pcmChunks = [];
  let pcmSampleRate = 0;

  async function start(){
    if (!navigator.mediaDevices || !navigator.mediaDevices.getUserMedia) throw new Error('getUserMedia not supported');
    if (typeof MediaRecorder === 'undefined') throw new Error('MediaRecorder not supported in this browser');
    stream = await navigator.mediaDevices.getUserMedia({ audio: true });
    // Choose a supported container; prefer webm/opus, fall back to ogg/opus, or let browser decide
    let options = {};
    try {
      if (MediaRecorder.isTypeSupported && MediaRecorder.isTypeSupported('audio/webm;codecs=opus')) {
        options.mimeType = 'audio/webm;codecs=opus';
      } else if (MediaRecorder.isTypeSupported && MediaRecorder.isTypeSupported('audio/ogg;codecs=opus')) {
        options.mimeType = 'audio/ogg;codecs=opus';
      }
    } catch { /* ignore */ }
    mediaRecorder = new MediaRecorder(stream, options);
    chunks = [];
    mediaRecorder.ondataavailable = e => { if (e.data && e.data.size > 0) chunks.push(e.data); };
    // Use a small timeslice to ensure dataavailable fires periodically on some browsers
    mediaRecorder.start(250);

    // Start PCM fallback in parallel to guarantee capture on browsers where MediaRecorder yields no data
    try {
      pcmCtx = new (window.AudioContext || window.webkitAudioContext)();
      await pcmCtx.resume();
      pcmSource = pcmCtx.createMediaStreamSource(stream);
      const bufferSize = 4096;
      const inputChannels = Math.min(2, pcmSource.channelCount || 2);
      pcmProcessor = pcmCtx.createScriptProcessor(bufferSize, inputChannels, 1);
      pcmChunks = [];
      pcmSampleRate = pcmCtx.sampleRate;
      pcmProcessor.onaudioprocess = function(ev){
        const ib = ev.inputBuffer;
        const frames = ib.length;
        const chs = ib.numberOfChannels || 1;
        let mono = new Float32Array(frames);
        if (chs === 1) {
          mono.set(ib.getChannelData(0));
        } else {
          const c0 = ib.getChannelData(0);
          const c1 = ib.getChannelData(1);
          for (let i=0;i<frames;i++){ mono[i] = (c0[i] + c1[i]) * 0.5; }
        }
        pcmChunks.push(mono);
      };
      pcmSource.connect(pcmProcessor);
      const gain = pcmCtx.createGain();
      gain.gain.value = 0.0;
      pcmProcessor.connect(gain);
      gain.connect(pcmCtx.destination);
    } catch (e) {
      console.warn('codeheroAudio: PCM fallback init failed', e);
    }
  }

  async function stop(){
    if (!mediaRecorder) return '';
    await new Promise(r => {
      try {
        mediaRecorder.onstop = r;
        try { mediaRecorder.requestData(); } catch { /* not supported everywhere */ }
        mediaRecorder.stop();
      } catch { r(); }
    });
    try { stream && stream.getTracks().forEach(t => t.stop()); } catch { /* no-op */ }
    // Stop PCM fallback path
    try {
      if (pcmProcessor) { pcmProcessor.disconnect(); pcmProcessor.onaudioprocess = null; }
      if (pcmSource) { pcmSource.disconnect(); }
      if (pcmCtx) { await pcmCtx.close(); }
    } catch {}
    if (!chunks || chunks.length === 0) { console.warn('codeheroAudio: no chunks captured'); return ''; }
    const type = (chunks[0] && chunks[0].type) || 'audio/webm';
    const blob = new Blob(chunks, { type });
    const buffer = await blob.arrayBuffer();
    // Convert to mono 16k WAV via Web Audio resampling
    const wav = await webmToWav(buffer);
    // Encode ArrayBuffer to base64 in chunks to avoid call stack limits
    const b64 = toBase64(wav);
    // reset state
    chunks = [];
    mediaRecorder = undefined;
    stream = undefined;
    pcmChunks = [];
    pcmProcessor = null; pcmSource = null; pcmCtx = null; pcmSampleRate = 0;
    return b64;
  }

  // Streaming-friendly variant: returns a Blob of WAV instead of base64
  async function stopAsBlob(){
    if (!mediaRecorder) return null;
    await new Promise(r => {
      try {
        mediaRecorder.onstop = r;
        try { mediaRecorder.requestData(); } catch {}
        mediaRecorder.stop();
      } catch { r(); }
    });
    try { stream && stream.getTracks().forEach(t => t.stop()); } catch {}
    try {
      if (pcmProcessor) { pcmProcessor.disconnect(); pcmProcessor.onaudioprocess = null; }
      if (pcmSource) { pcmSource.disconnect(); }
      if (pcmCtx) { await pcmCtx.close(); }
    } catch {}

    let wavBuffer = null;
    if (chunks && chunks.length > 0) {
      try {
        const type = (chunks[0] && chunks[0].type) || 'audio/webm';
        const blob = new Blob(chunks, { type });
        const buffer = await blob.arrayBuffer();
        wavBuffer = await webmToWav(buffer);
      } catch (e) {
        console.warn('codeheroAudio: media recorder decode failed, trying PCM fallback', e);
      }
    }
    if (!wavBuffer && pcmChunks && pcmChunks.length > 0) {
      try {
        const merged = mergeFloat32(pcmChunks);
        const resampled = resampleTo16k(merged, pcmSampleRate || 48000);
        wavBuffer = encodeWav(resampled, 16000);
      } catch (e) { console.warn('codeheroAudio: PCM fallback failed', e); }
    }
    // reset
    chunks = [];
    mediaRecorder = undefined;
    stream = undefined;
    pcmChunks = [];
    pcmProcessor = null; pcmSource = null; pcmCtx = null; pcmSampleRate = 0;
    return wavBuffer ? new Blob([wavBuffer], { type: 'audio/wav' }) : null;
  }

  // Capability/provision diagnostics for UI
  async function support(){
    const secure = location.protocol === 'https:' || location.hostname === 'localhost';
    const hasMediaDevices = !!(navigator.mediaDevices);
    const hasGetUserMedia = !!(navigator.mediaDevices && navigator.mediaDevices.getUserMedia);
    const hasMediaRecorder = typeof MediaRecorder !== 'undefined';
    let preferred = null;
    try {
      if (hasMediaRecorder && MediaRecorder.isTypeSupported && MediaRecorder.isTypeSupported('audio/webm;codecs=opus')) preferred = 'audio/webm;codecs=opus';
      else if (hasMediaRecorder && MediaRecorder.isTypeSupported && MediaRecorder.isTypeSupported('audio/ogg;codecs=opus')) preferred = 'audio/ogg;codecs=opus';
    } catch {}
    let micPerm = 'unknown';
    try {
      if (navigator.permissions && navigator.permissions.query) {
        const res = await navigator.permissions.query({ name: 'microphone' });
        micPerm = res.state; // 'granted' | 'denied' | 'prompt'
      }
    } catch {}
    return { secure, hasMediaDevices, hasGetUserMedia, hasMediaRecorder, preferred, micPerm };
  }

  async function load(audioEl, src){
    if (!audioEl) return;
    await new Promise((resolve, reject) => {
      const cleanup = () => {
        audioEl.oncanplaythrough = null; audioEl.onerror = null; audioEl.onloadeddata = null;
      };
      audioEl.oncanplaythrough = () => { cleanup(); resolve(); };
      audioEl.onloadeddata = () => { cleanup(); resolve(); };
      audioEl.onerror = (e) => { cleanup(); reject(e); };
      audioEl.src = src;
      try { audioEl.load(); } catch { /* some browsers auto-load */ }
    });
  }

  // Minimal decode->WAV encode using Web Audio API
  async function webmToWav(arrayBuffer){
    // Decode in a regular AudioContext (browser default rate), then resample offline to 16k mono
    const decodeCtx = new (window.AudioContext || window.webkitAudioContext)();
    const audio = await decodeCtx.decodeAudioData(arrayBuffer.slice(0));
    try { await decodeCtx.close(); } catch { /* ignore */ }

    const targetRate = 16000;
    const length = Math.ceil(audio.duration * targetRate);
    const offline = new OfflineAudioContext(1, length, targetRate);
    const source = offline.createBufferSource();
    // Mix to mono if needed
    const monoBuf = offline.createBuffer(1, audio.length, audio.sampleRate);
    const tmp = new Float32Array(audio.length);
    for (let c = 0; c < audio.numberOfChannels; c++) {
      const data = audio.getChannelData(c);
      for (let i = 0; i < data.length; i++) { tmp[i] += data[i] / audio.numberOfChannels; }
    }
    monoBuf.copyToChannel(tmp, 0);
    source.buffer = monoBuf;
    source.connect(offline.destination);
    source.start(0);
    const rendered = await offline.startRendering();
    const pcm = rendered.getChannelData(0);
    return encodeWav(pcm, targetRate);
  }

  function encodeWav(samples, sampleRate){
    const buffer = new ArrayBuffer(44 + samples.length * 2);
    const view = new DataView(buffer);

    function writeString(view, offset, string){ for (let i=0;i<string.length;i++){ view.setUint8(offset+i, string.charCodeAt(i)); } }
    function floatTo16BitPCM(view, offset, input){ for (let i=0;i<input.length;i++, offset+=2){ let s = Math.max(-1, Math.min(1, input[i])); view.setInt16(offset, s<0?s*0x8000:s*0x7FFF, true);} }

    writeString(view, 0, 'RIFF');
    view.setUint32(4, 36 + samples.length * 2, true);
    writeString(view, 8, 'WAVE');
    writeString(view, 12, 'fmt ');
    view.setUint32(16, 16, true);
    view.setUint16(20, 1, true);
    view.setUint16(22, 1, true);
    view.setUint32(24, sampleRate, true);
    view.setUint32(28, sampleRate * 2, true);
    view.setUint16(32, 2, true);
    view.setUint16(34, 16, true);
    writeString(view, 36, 'data');
    view.setUint32(40, samples.length * 2, true);
    floatTo16BitPCM(view, 44, samples);
    return buffer;
  }

  function mergeFloat32(chunks){
    let total = 0; for (const c of chunks) total += c.length;
    const out = new Float32Array(total);
    let offset = 0; for (const c of chunks){ out.set(c, offset); offset += c.length; }
    return out;
  }

  function resampleTo16k(input, inRate){
    if (inRate === 16000) return input;
    const ratio = inRate / 16000;
    const newLen = Math.round(input.length / ratio);
    const out = new Float32Array(newLen);
    for (let i=0;i<newLen;i++){
      const idx = i * ratio;
      const i0 = Math.floor(idx);
      const i1 = Math.min(i0 + 1, input.length - 1);
      const frac = idx - i0;
      out[i] = input[i0] * (1 - frac) + input[i1] * frac;
    }
    return out;
  }

  function toBase64(arrayBuffer){
    const bytes = new Uint8Array(arrayBuffer);
    const chunk = 0x8000; // 32KB chunks to avoid call stack overflow
    let binary = '';
    for (let i = 0; i < bytes.length; i += chunk){
      const sub = bytes.subarray(i, i + chunk);
      binary += String.fromCharCode.apply(null, sub);
    }
    return btoa(binary);
  }

  return { start, stop, stopAsBlob, load, support };
})();
