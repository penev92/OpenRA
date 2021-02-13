function populatePalettes() {
    let select = document.getElementById("selectPalette");
    select.innerHTML = null;

    let palettes = JSON.parse(localStorage.getItem("palettes"));
    if (palettes !== null) {
        palettes.forEach(paletteName => {
            let element = document.createElement("option");
            element.textContent = paletteName;
            element.value = paletteName;
            select.appendChild(element);
        });
    }
};

function readStream(readableStream) {
    const reader = readableStream.getReader();
    let charsReceived = 0;

    // read() returns a promise that resolves
    // when a value has been received
    reader.read().then(
        function processText({ done, value }) {
            // Result objects contain two properties:
            // done  - true if the stream has already given you all its data.
            // value - some data. Always undefined when done is true.
            if (done) {
                // Do stuff here?
                return;
            }

            charsReceived += value.length;
            const chunk = value;
            startAudioStream(chunk);

            // Read some more, and call this function again
            return reader.read().then(processText);
        }
    );
};

function startAudioStream(rawBytes, sampleRate, latency) {
    let arrayBuffer = new ArrayBuffer(rawBytes.length);
    let floatArray = new Float32Array(arrayBuffer);
    let byteArray = new Uint8Array(arrayBuffer);

    rawBytes.forEach(function (b, i) {
        byteArray[i] = b;
    });

    let AudioContext = window.AudioContext || window.webkitAudioContext;
    let context = new AudioContext();
    let nextTime = 0;

    // function update(byteArray) {
        // buffer = context.createBuffer(1, byteArray.byteLength / 4, sampleRate);
        let buffer = context.createBuffer(2, floatArray.length / 2, 22050);

        buffer.copyToChannel(floatArray.filter((element, index) => { return index % 2 === 0; }), 0);
        buffer.copyToChannel(floatArray.filter((element, index) => { return index % 2 !== 0; }), 1);

        let source = context.createBufferSource();
        source.buffer = buffer;

        //if (nextTime == 0)
        //    nextTime = context.currentTime + latency;

        source.connect(context.destination);
        source.start();
        //nextTime += buffer.duration;
    // };

    // audioClient = new BinaryClient(websocketAddress);
    // audioClient.on('stream', function(stream, meta) {
    //     stream.on('data', function(data) {
    //         update(new Float32Array(data));
    //     });
    // });
};
