let wsocket;
let container;
let scene;
let camera;
let controls;
let mesh;
let mousePressed = false;

function initThreeJS (){
    container = document.querySelector( '#scene-container' );

    // scene
    scene = new THREE.Scene();
    scene.background = new THREE.Color( 0x7FBCDD );
    // camera
    camera = new THREE.PerspectiveCamera(35, container.clientWidth / container.clientHeight, 0.1, 100,);
    camera.position.set( -4, 4, 10 );
    // controls
    controls = new THREE.OrbitControls( camera, container );
    // mesh
    const geometry = new THREE.BoxBufferGeometry( 2, 2, 2 );
    const material = new THREE.MeshStandardMaterial( { color: 0x005000 } );
    mesh = new THREE.Mesh( geometry, material );
    scene.add( mesh );
    // light
    const ambientLight = new THREE.HemisphereLight(0xffaaff, 0x202020, 6);
    const mainLight = new THREE.DirectionalLight( 0xffffff, 4 );
    mainLight.position.set( 10, 10, 10 );
    scene.add( ambientLight, mainLight );
    // renderer
    renderer = new THREE.WebGLRenderer( { antialias: true } );
    renderer.setSize( container.clientWidth, container.clientHeight );
    renderer.setPixelRatio( window.devicePixelRatio );
    container.appendChild( renderer.domElement );

    // start the animation loop
    renderer.setAnimationLoop( () => {
        renderer.render( scene, camera );
    } );
}

function initMouseEvents (){
    container = document.querySelector( '#scene-container' );
    container.onmousemove = function(){
        if(mousePressed){
            sendAngles(controls.getAzimuthalAngle(), controls.getPolarAngle());
        }
    };
    container.onmousedown = function(){
        mousePressed = true;
    }
    container.onmouseup = function(){
        mousePressed = false;
    }
}

function initWebSocket (addr){
    wsocket = new WebSocket(addr);
    // set callbacks
    wsocket.onopen = () => {
        console.log("websocket onopen");
        $.notify("Connected to server", "success");
    }
    wsocket.onerror = error => {
        console.log(error);
        $.notify("WebSocket Error", "error");
    }
    wsocket.onclose = e => {
        console.log(e);
        let msg = e.reason ? "connection closed with the reason: " + e.reason : "connection closed without any reason";
        $.notify(msg, "warn");
    }
    wsocket.onmessage = (e) => {
        console.log("message received from server");
        console.log(e.data);
    }
}

function sendAngles(azimuth, elevation){
    if(wsocket.readyState === WebSocket.OPEN){
        wsocket.send(JSON.stringify({
            az: azimuth, 
            el: elevation
        }));
    }
}

$(document).ready(function () {
    // more here: https://notifyjs.jpillora.com
    $.notify.defaults( {globalPosition: "top left"} );

    initThreeJS();
    initMouseEvents();
    initWebSocket("ws://127.0.0.1:55555/Laputa");

});