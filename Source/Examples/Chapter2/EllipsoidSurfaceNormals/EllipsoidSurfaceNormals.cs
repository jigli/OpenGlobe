﻿#region License
//
// (C) Copyright 2010 Patrick Cozzi and Deron Ohlarik
//
// Distributed under the Boost Software License, Version 1.0.
// See License.txt or http://www.boost.org/LICENSE_1_0.txt.
//
#endregion

//
// Keyboards Controls
//   Up - Increase semi-minor axis
//   Down - Decrease semi-minor axis
//

using System;
using System.Drawing;

using MiniGlobe.Core;
using MiniGlobe.Core.Geometry;
using MiniGlobe.Core.Tessellation;
using MiniGlobe.Renderer;
using MiniGlobe.Scene;
using OpenTK;

namespace MiniGlobe.Examples.Chapter2
{
    sealed class EllipsoidSurfaceNormals : IDisposable
    {
        public EllipsoidSurfaceNormals()
        {
            _window = Device.CreateWindow(800, 600, "Chapter 2:  Ellipsoid Surface Normals");
            _window.Resize += OnResize;
            _window.RenderFrame += OnRenderFrame;
            _window.Keyboard.KeyDown += OnKeyDown;
            _sceneState = new SceneState();
            _camera = new CameraLookAtPoint(_sceneState.Camera, _window, Ellipsoid.UnitSphere);

            CreateScene();

            ///////////////////////////////////////////////////////////////////

            _sceneState.Camera.Eye = Vector3D.UnitY;
            _sceneState.Camera.ZoomToTarget(2);
            PersistentView.Execute(@"E:\Dropbox\My Dropbox\Book\Manuscript\VirtualGlobeFoundations\Figures\EllipsoidSurfaceNormals.xml", _window, _sceneState.Camera);

            HighResolutionSnap snap = new HighResolutionSnap(_window, _sceneState);
            snap.ColorFilename = @"E:\Dropbox\My Dropbox\Book\Manuscript\VirtualGlobeFoundations\Figures\EllipsoidSurfaceNormals1.png";
            snap.WidthInInches = 1.5;
            snap.DotsPerInch = 1200;
        }

        private void OnResize()
        {
            _window.Context.Viewport = new Rectangle(0, 0, _window.Width, _window.Height);
            _sceneState.Camera.AspectRatio = _window.Width / (double)_window.Height;
        }

        private void OnRenderFrame()
        {
            Context context = _window.Context;
            context.Clear(ClearBuffers.ColorAndDepthBuffer, Color.White, 1, 0);
            context.Bind(_renderState);
            context.Bind(_sp);
            context.Bind(_va);
            context.Draw(_primitiveType, _sceneState);

            _wireframe.Render(_sceneState);
            _axes.Render(_sceneState);
            _normals.Render(_sceneState);
            _geodeticBillboard.Render(_sceneState);
            _geocentricBillboard.Render(_sceneState);
            _tangentPlane.Render(_sceneState);
        }

        private void OnKeyDown(object sender, KeyboardKeyEventArgs e)
        {
            if ((e.Key == KeyboardKey.Up) || (e.Key == KeyboardKey.Down))
            {
                if (e.Key == KeyboardKey.Up)
                {
                    _semiMinorAxis = Math.Min(_semiMinorAxis + _semiMinorAxisDelta, 1.0);
                }
                else
                {
                    _semiMinorAxis = Math.Max(_semiMinorAxis - _semiMinorAxisDelta, 0.1);
                }

                CreateScene();
            }
        }

        private void CreateScene()
        {
            DisposeScene();

            Ellipsoid globeShape = new Ellipsoid(1, 1, _semiMinorAxis);

            string vs =
                @"#version 150

                  in vec4 position;
                  out vec3 worldPosition;
                  out vec3 positionToLight;
                  out vec3 positionToEye;

                  uniform mat4 mg_modelViewPerspectiveProjectionMatrix;
                  uniform vec3 mg_cameraEye;
                  uniform vec3 mg_cameraLightPosition;

                  void main()                     
                  {
                        gl_Position = mg_modelViewPerspectiveProjectionMatrix * position; 

                        worldPosition = position.xyz;
                        positionToLight = mg_cameraLightPosition - worldPosition;
                        positionToEye = mg_cameraEye - worldPosition;
                  }";

            string fs =
                @"#version 150
                 
                  in vec3 worldPosition;
                  in vec3 positionToLight;
                  in vec3 positionToEye;
                  out vec3 fragmentColor;

                  uniform vec4 mg_diffuseSpecularAmbientShininess;
                  uniform vec3 u_globeOneOverRadiiSquared;

                  float LightIntensity(vec3 normal, vec3 toLight, vec3 toEye, vec4 diffuseSpecularAmbientShininess)
                  {
                      vec3 toReflectedLight = reflect(-toLight, normal);

                      float diffuse = max(dot(toLight, normal), 0.0);
                      float specular = max(dot(toReflectedLight, toEye), 0.0);
                      specular = pow(specular, diffuseSpecularAmbientShininess.w);

                      return (diffuseSpecularAmbientShininess.x * diffuse) +
                             (diffuseSpecularAmbientShininess.y * specular) +
                              diffuseSpecularAmbientShininess.z;
                  }

                  vec3 ComputeDeticSurfaceNormal(vec3 positionOnEllipsoid, vec3 oneOverEllipsoidRadiiSquared)
                  {
                      return normalize(positionOnEllipsoid * oneOverEllipsoidRadiiSquared);
                  }

                  void main()
                  {
                      vec3 normal = ComputeDeticSurfaceNormal(worldPosition, u_globeOneOverRadiiSquared);
                      float intensity = LightIntensity(normal,  normalize(positionToLight), normalize(positionToEye), mg_diffuseSpecularAmbientShininess);
                      fragmentColor = vec3(intensity, 0, intensity);
                  }";
            _sp = Device.CreateShaderProgram(vs, fs);
            (_sp.Uniforms["u_globeOneOverRadiiSquared"] as Uniform<Vector3>).Value = Conversion.ToVector3(globeShape.OneOverRadiiSquared);

            Mesh mesh = GeographicGridEllipsoidTessellator.Compute(globeShape, 64, 32, GeographicGridEllipsoidVertexAttributes.Position);
            _va = _window.Context.CreateVertexArray(mesh, _sp.VertexAttributes, BufferHint.StaticDraw);
            _primitiveType = mesh.PrimitiveType;

            _renderState = new RenderState();
            _renderState.FacetCulling.FrontFaceWindingOrder = mesh.FrontFaceWindingOrder;

            ///////////////////////////////////////////////////////////////////

            _wireframe = new Wireframe(_window.Context, mesh);
            _wireframe.Width = 2;

            ///////////////////////////////////////////////////////////////////

            _axes = new Axes(_window.Context);
            _axes.Width = 3;
            _axes.Length = 1.5;

            ///////////////////////////////////////////////////////////////////

            Vector3D p = globeShape.ToVector3D(new Geodetic3D(0, Trig.ToRadians(45), 0));
            Vector3D deticNormal = globeShape.DeticSurfaceNormal(p);
            Vector3D centricNormal = Ellipsoid.CentricSurfaceNormal(p);

            double normalLength = globeShape.MaximumRadius;
            Vector3D pDetic = p + (normalLength * deticNormal);
            Vector3D pCentric = p + (normalLength * centricNormal);

            Vector3[] normalPositions = new Vector3[] 
            { 
                Conversion.ToVector3(p),
                Conversion.ToVector3(pDetic),
                Conversion.ToVector3(p),
                Conversion.ToVector3(pCentric)
            };

            BlittableRGBA[] normalColors = new BlittableRGBA[] 
            { 
                new BlittableRGBA(Color.DarkGreen),
                new BlittableRGBA(Color.DarkGreen),
                new BlittableRGBA(Color.DarkCyan),
                new BlittableRGBA(Color.DarkCyan),
            };

            _normals = new Polyline(_window.Context, normalPositions, normalColors);
            _normals.Width = 3;

            ///////////////////////////////////////////////////////////////////
            Font font = new Font("Arial", 24);

            _geodeticBillboard = new BillboardGroup(_window.Context, 
                new[] { Conversion.ToVector3(pDetic) },
                Device.CreateBitmapFromText("Geodetic", font));
            _geodeticBillboard.Color = Color.DarkGreen;

            _geocentricBillboard = new BillboardGroup(_window.Context,
                new[] { Conversion.ToVector3(pCentric) },
                Device.CreateBitmapFromText("Geocentric", font));
            _geocentricBillboard.Color = Color.DarkCyan;

            font.Dispose();

            ///////////////////////////////////////////////////////////////////
            Vector3D east = Vector3D.UnitZ.Cross(deticNormal);
            Vector3D north = deticNormal.Cross(east);

            _tangentPlane = new Plane(_window.Context, p, east, north);
            _tangentPlane.OutlineWidth = 3;
        }

        private void DisposeScene()
        {
            if (_sp != null)
            {
                _sp.Dispose();
            }

            if (_va != null)
            {
                _va.Dispose();
            }

            if (_wireframe != null)
            {
                _wireframe.Dispose();
            }

            if (_axes != null)
            {
                _axes.Dispose();
            }

            if (_geodeticBillboard != null)
            {
                _geodeticBillboard.Dispose();
            }

            if (_geocentricBillboard != null)
            {
                _geocentricBillboard.Dispose();
            }

            if (_normals != null)
            {
                _normals.Dispose();
            }

            if (_tangentPlane != null)
            {
                _tangentPlane.Dispose();
            }
        }

        #region IDisposable Members

        public void Dispose()
        {
            _window.Dispose();
            _camera.Dispose();
            DisposeScene();
        }

        #endregion

        private void Run(double updateRate)
        {
            _window.Run(updateRate);
        }

        static void Main()
        {
            using (EllipsoidSurfaceNormals example = new EllipsoidSurfaceNormals())
            {
                example.Run(30.0);
            }
        }

        private readonly MiniGlobeWindow _window;
        private readonly SceneState _sceneState;
        private readonly CameraLookAtPoint _camera;

        private RenderState _renderState;
        private ShaderProgram _sp;
        private VertexArray _va;
        private PrimitiveType _primitiveType;

        private Wireframe _wireframe;
        private Axes _axes;
        private BillboardGroup _geodeticBillboard;
        private BillboardGroup _geocentricBillboard;
        private Polyline _normals;
        private Plane _tangentPlane;

        private double _semiMinorAxis = 0.7;
        private const double _semiMinorAxisDelta = 0.025;
    }
}