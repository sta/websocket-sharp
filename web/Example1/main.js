let connection;
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

function sendAngles(azimuth, elevation){
    // todo: send this stuff over websocket to the server
    console.log(azimuth + '   ' + elevation);
}

$(document).ready(function () {
    console.log("go");

    initThreeJS();
    initMouseEvents();

    connection = new WebSocket("ws://127.0.0.1:55555/Laputa");
    connection.onopen = () => {
        console.log("opened");
        connection.send('BALUS');
    }

    connection.onerror = error => {
        console.log("WebSocket error");
        console.log(error);
    }

    connection.onclose = e => {
        console.log("connection closes");
        console.log(e);
    }

    connection.onmessage = (e) => {
        console.log(e.data);
    }

});