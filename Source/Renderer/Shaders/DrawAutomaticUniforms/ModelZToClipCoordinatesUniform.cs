﻿#region License
//
// (C) Copyright 2010 Patrick Cozzi and Deron Ohlarik
//
// Distributed under the Boost Software License, Version 1.0.
// See License.txt or http://www.boost.org/LICENSE_1_0.txt.
//
#endregion

using OpenGlobe.Core;

namespace OpenGlobe.Renderer
{
    internal class ModelZToClipCoordinatesUniform : DrawAutomaticUniform
    {
        public ModelZToClipCoordinatesUniform(Uniform uniform)
        {
            _uniform = (Uniform<Matrix42<float>>)uniform;
        }

        #region DrawAutomaticUniform Members

        public override void Set(Context context, DrawState drawState, SceneState sceneState)
        {
            _uniform.Value = Matrix42<float>.DoubleToFloat(sceneState.ModelZToClipCoordinates);
        }

        #endregion

        private Uniform<Matrix42<float>> _uniform;
    }
}
