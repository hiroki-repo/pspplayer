﻿// ----------------------------------------------------------------------------
// PSP Player Emulation Suite
// Copyright (C) 2008 Ben Vanik (noxa)
// Licensed under the LGPL - see License.txt in the project root for details
// ----------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Text;
using System.Threading;
using Tao.OpenGl;
using Tao.Platform.Windows;
using Noxa.Emulation.Psp.Cpu;
using System.Collections;

namespace Noxa.Emulation.Psp.Video.ManagedGL
{
	unsafe partial class MGLDriver
	{
		enum StateRequest
		{
			All,
			Drawing,
		}

		private static class FeatureState
		{
			public const int CullFace = 0;
			public const int DepthTest = 1;
			public const int AlphaTest = 2;
			public const int Fog = 3;
			public const int AlphaBlend = 4;
			public const int Textures = 5;
			public const int ClampToEdge = 6;

			public const uint CullFaceMask = 0x1 << CullFace;
			public const uint DepthTestMask = 0x1 << DepthTest;
			public const uint AlphaTestMask = 0x1 << AlphaTest;
			public const uint FogMask = 0x1 << Fog;
			public const uint AlphaBlendMask = 0x1 << AlphaBlend;
			public const uint TexturesMask = 0x1 << Textures;
			public const uint ClampToEdgeMask = 0x1 << ClampToEdge;
		}

		private static class ArrayState
		{
			public const int VertexArray = 0;
			public const int NormalArray = 1;
			public const int TextureCoordArray = 2;
			public const int ColorArray = 3;

			public const uint VertexArrayMask = 0x1 << VertexArray;
			public const uint NormalArrayMask = 0x1 << NormalArray;
			public const uint TextureCoordArrayMask = 0x1 << TextureCoordArray;
			public const uint ColorArrayMask = 0x1 << ColorArray;
		}

		private uint _featureStateValue;
		private uint _arrayStateValue;

		private bool _matricesValid;
		private int _currentTextureId;
		private int _currentProgramId;

		private void UpdateState( StateRequest request )
		{
			// Matrix update
			if( _matricesValid == false )
			{
				// Upload proj/view only - world goes to the shaders
				Gl.glMatrixMode( Gl.GL_PROJECTION );
				fixed( float* p = &_ctx.ProjectionMatrix[ 0 ] )
					Gl.glLoadMatrixf( ( IntPtr )p );
				Gl.glMatrixMode( Gl.GL_MODELVIEW );
				fixed( float* p = &_ctx.ViewMatrix[ 0 ] )
					Gl.glLoadMatrixf( ( IntPtr )p );

				// ?
				Gl.glMatrixMode( Gl.GL_TEXTURE );
				fixed( float* p = &_ctx.TextureMatrix[ 0 ] )
					Gl.glLoadMatrixf( ( IntPtr )p );

				_matricesValid = true;
			}
		}

		private void InvalidateMatrices()
		{
			_matricesValid = false;

			// TODO: Do this for all programs
			_defaultProgram.IsDirty = true;
		}

		internal void InvalidateCurrentTexture()
		{
			_currentTextureId = 0;
		}

		private void SetState( uint mask, uint values )
		{
			uint diff = ( _featureStateValue & mask ) ^ values;
			if( diff == 0 )
				return;

			if( ( diff & FeatureState.CullFaceMask ) != 0 )
			{
				if( ( values & FeatureState.CullFaceMask ) != 0 )
					Gl.glEnable( Gl.GL_CULL_FACE );
				else
					Gl.glDisable( Gl.GL_CULL_FACE );
			}
			if( ( diff & FeatureState.DepthTestMask ) != 0 )
			{
				if( ( values & FeatureState.DepthTestMask ) != 0 )
					Gl.glEnable( Gl.GL_DEPTH_TEST );
				else
					Gl.glDisable( Gl.GL_DEPTH_TEST );
			}
			if( ( diff & FeatureState.AlphaTestMask ) != 0 )
			{
				if( ( values & FeatureState.AlphaTestMask ) != 0 )
					Gl.glEnable( Gl.GL_ALPHA_TEST );
				else
					Gl.glDisable( Gl.GL_ALPHA_TEST );
			}
			if( ( diff & FeatureState.FogMask ) != 0 )
			{
				if( ( values & FeatureState.FogMask ) != 0 )
					Gl.glEnable( Gl.GL_FOG );
				else
					Gl.glDisable( Gl.GL_FOG );
			}
			if( ( diff & FeatureState.AlphaBlendMask ) != 0 )
			{
				if( ( values & FeatureState.AlphaBlendMask ) != 0 )
					Gl.glEnable( Gl.GL_BLEND );
				else
					Gl.glDisable( Gl.GL_BLEND );
			}
			if( ( diff & FeatureState.TexturesMask ) != 0 )
			{
				if( ( values & FeatureState.TexturesMask ) != 0 )
				{
					_ctx.TexturesEnabled = true;
					_defaultProgram.IsDirty = true;
					Gl.glEnable( Gl.GL_TEXTURE_2D );
				}
				else
				{
					_ctx.TexturesEnabled = false;
					_defaultProgram.IsDirty = true;
					Gl.glDisable( Gl.GL_TEXTURE_2D );
				}
			}
			if( ( diff & FeatureState.ClampToEdgeMask ) != 0 )
			{
				if( ( values & FeatureState.ClampToEdgeMask ) != 0 )
					Gl.glEnable( Gl.GL_CLAMP_TO_EDGE );
				else
					Gl.glDisable( Gl.GL_CLAMP_TO_EDGE );
			}

			_featureStateValue = ( _featureStateValue & ~mask ) | values;
		}

		private void EnableArrays( uint values )
		{
			if( ( _arrayStateValue & ArrayState.VertexArrayMask ) != ( values & ArrayState.VertexArrayMask ) )
			{
				if( ( values & ArrayState.VertexArrayMask ) != 0 )
					Gl.glEnableClientState( Gl.GL_VERTEX_ARRAY );
				else
					Gl.glDisableClientState( Gl.GL_VERTEX_ARRAY );
			}
			if( ( _arrayStateValue & ArrayState.NormalArrayMask ) != ( values & ArrayState.NormalArrayMask ) )
			{
				if( ( values & ArrayState.NormalArrayMask ) != 0 )
					Gl.glEnableClientState( Gl.GL_NORMAL_ARRAY );
				else
					Gl.glDisableClientState( Gl.GL_NORMAL_ARRAY );
			}
			if( ( _arrayStateValue & ArrayState.TextureCoordArrayMask ) != ( values & ArrayState.TextureCoordArrayMask ) )
			{
				if( ( values & ArrayState.TextureCoordArrayMask ) != 0 )
					Gl.glEnableClientState( Gl.GL_TEXTURE_COORD_ARRAY );
				else
					Gl.glDisableClientState( Gl.GL_TEXTURE_COORD_ARRAY );
			}
			if( ( _arrayStateValue & ArrayState.ColorArrayMask ) != ( values & ArrayState.ColorArrayMask ) )
			{
				if( ( values & ArrayState.ColorArrayMask ) != 0 )
					Gl.glEnableClientState( Gl.GL_COLOR_ARRAY );
				else
					Gl.glDisableClientState( Gl.GL_COLOR_ARRAY );
			}
			_arrayStateValue = values;
		}

		private void SetTextures()
		{
			MGLTextureInfo info = _ctx.Textures[ 0 ];
			if( info.Address == 0 )
			{
				if( _currentTextureId != 0 )
					Gl.glBindTexture( Gl.GL_TEXTURE_2D, 0 );
				_currentTextureId = 0;
				return;
			}

			// Check valid
			bool valid = !( ( info.Address == 0x0 ) || ( info.LineWidth == 0x0 ) || ( info.Width == 0 ) || ( info.Height == 0 ) );
			// TODO: from framebuffer? - make sure this check is still valid!
			valid = valid && !( ( info.Address == 0x0400000 ) && ( info.LineWidth == 0x4 ) && ( info.Width == 0x2 ) && ( info.Height == 0x2 ) );

			// Check cache
			uint checksum;
			MGLTexture texture = _ctx.TextureCache.Find( info, out checksum );

			// If found in cache, set and return
			if( texture != null )
			{
				if( _currentTextureId != texture.TextureID )
				{
					Gl.glBindTexture( Gl.GL_TEXTURE_2D, texture.TextureID );
					_currentTextureId = texture.TextureID;

					// HACK: required to get textures to work right - does something after the binding so that things show up
					Gl.glTexParameteri( Gl.GL_TEXTURE_2D, Gl.GL_TEXTURE_MIN_FILTER, _ctx.TextureMinFilter );
					//Gl.glTexParameteri( Gl.GL_TEXTURE_2D, Gl.GL_TEXTURE_MAG_FILTER, Gl.GL_NEAREST );

					// TODO: set all
					_defaultProgram.IsDirty = true;
				}
				return;
			}

			// Not found - create
			texture = MGLTexture.LoadTexture( this, _ctx, info, checksum );
			_currentTextureId = texture.TextureID;

			_ctx.TextureCache.Add( texture );

			// TODO: set all
			_defaultProgram.IsDirty = true;
		}

		private void SetNoProgram()
		{
			if( _currentProgramId != 0 )
				Gl.glUseProgram( 0 );
			_currentProgramId = 0;
		}

		private void SetDefaultProgram( bool isTransformed, uint colorType, uint boneCount, uint morphCount )
		{
			if( _currentProgramId != _defaultProgram.ProgramID )
				Gl.glUseProgram( _defaultProgram.ProgramID );
			_currentProgramId = _defaultProgram.ProgramID;
			_defaultProgram.Setup( _ctx, isTransformed, colorType, boneCount, morphCount );
		}
	}
}
