const errorChart = new Chart(document.getElementById("error-chart"), {
    type: "line",
    options: {
        responsive: true,
        animation: {
            duration: 0
        }
    },
    data: {
        labels: [],
        datasets: [
            {
                label: "P Error",
                data: [],
                fill: false,
                borderColor: 'rgb(75, 192, 192)'
            },
            {
                label: "I Error",
                data: [],
                fill: false,
                borderColor: 'rgb(192, 75, 75)'
            },
            {
                label: "D Error",
                data: [],
                fill: false,
                borderColor: 'rgb(75, 75, 192)'
            }
        ]
    }
});

const speedChart = new Chart(document.getElementById("speed-chart"), {
    type: "line",
    options: {
        responsive: true,
        animation: {
            duration: 0
        }
    },
    data: {
        labels: [],
        datasets: [
            {
                label: "Right",
                data: [],
                fill: false,
                borderColor: 'rgb(75, 192, 192)'
            },
            {
                label: "Left",
                data: [],
                fill: false,
                borderColor: 'rgb(192, 75, 75)'
            }
        ]
    }
});

const locationChart = new Chart(document.getElementById("location-chart"), {
    type: "line",
    options: {
        responsive: true,
        animation: {
            duration: 0
        }
    },
    data: {
        labels: [],
        datasets: [{
            label: "Line X",
            data: [],
            fill: false,
            borderColor: 'rgb(75, 192, 192)'
        }]
    }
});

setInterval(() => {
    errorChart.update();
    locationChart.update();
    speedChart.update();
}, 1000);

const websocket = new WebSocket("ws://localhost:8080");

const inputNames = [
    ["base-speed", "baseSpeed"],
    ["target-x", "targetX"],
    ["p-sensitivity", "pSensitivity"],
    ["i-sensitivity", "iSensitivity"],
    ["d-sensitivity", "dSensitivity"],
    ["look-ahead-sensitivity-dropoff", "lookAheadSensitivityDropoff"]
];

websocket.addEventListener("message", e => {
    const data = JSON.parse(e.data);

    if (data.type == "location") {
        locationChart.data.labels.push(data.time);
        locationChart.data.datasets[0].data.push(data.value);
    }
    else if (data.type == "speed") {
        speedChart.data.labels.push(data.time);
        speedChart.data.datasets[0].data.push(data.value.right);
        speedChart.data.datasets[1].data.push(data.value.left);
    }
    else if (data.type == "error") {
        errorChart.data.labels.push(data.time);
        errorChart.data.datasets[0].data.push(data.value.p);
        errorChart.data.datasets[1].data.push(data.value.i);
        errorChart.data.datasets[2].data.push(data.value.d);
    }
    else
    {
        for (const [name, key] in object) {
            if (data.type == key) {
                document.getElementById(name).value = data.value.toString();
            }
        }
    }
});

inputNames.forEach(([name, key]) => {
    const input = document.getElementById(name);

    input.addEventListener("input", e => {
        websocket.send(JSON.stringify({
            type: key,
            value: parseFloat(e.target.value)
        }));
    });
});

document.getElementById("pause").addEventListener("click", _ => {
    websocket.send(JSON.stringify({
        type: "pause"
    }));
});

document.getElementById("resume").addEventListener("click", _ => {
    websocket.send(JSON.stringify({
        type: "resume"
    }));
});
