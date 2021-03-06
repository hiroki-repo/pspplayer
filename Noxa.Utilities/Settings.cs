// ----------------------------------------------------------------------------
// Shared Utility Library
// Copyright (C) 2006 Ben Vanik (noxa)
// Licensed under the LGPL - see License.txt in the project root for details
// ----------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Text;
using System.Collections.ObjectModel;
using System.Xml;
using System.IO;

namespace Noxa
{
	/// <summary>
	/// Simple settings collection.
	/// </summary>
	public class Settings : ISettingsType
	{
		public const int MaximumDepth = 10;

		protected object _syncRoot = new object();
		protected Dictionary<string, object> _settings = new Dictionary<string, object>();

		/// <summary>
		/// Initializes a new <see cref="Settings"/> instance.
		/// </summary>
		public Settings()
		{
		}

		/// <summary>
		/// A list of all keys in this collection.
		/// </summary>
		public string[] Keys
		{
			get
			{
				lock( _syncRoot )
				{
					int n = 0;
					string[] keys = new string[ _settings.Keys.Count ];
					foreach( string key in _settings.Keys )
					{
						keys[ n ] = key;
						n++;
					}
					return keys;
				}
			}
		}

		/// <summary>
		/// Check to see if the collection contains the given setting.
		/// </summary>
		/// <param name="key">Key for the setting to check.</param>
		/// <returns><c>true</c> if the collection contains the given setting.</returns>
		public bool ContainsKey( string key )
		{
			lock( _syncRoot )
			{
				return _settings.ContainsKey( key );
			}
		}

		/// <summary>
		/// Get or set the value of the given key.
		/// </summary>
		/// <param name="key">Key of the setting to get or set.</param>
		/// <returns>The value of the setting or <c>null</c> if it is not found.</returns>
		public object this[ string key ]
		{
			get
			{
				lock( _syncRoot )
				{
					if( _settings.ContainsKey( key ) == true )
						return _settings[ key ];
					else
						return null;
				}
			}
			set
			{
				lock( _syncRoot )
				{
					_settings[ key ] = value;
				}
			}
		}

		/// <summary>
		/// Get a value for the setting with the given key.
		/// </summary>
		/// <typeparam name="T">Type of the setting.</typeparam>
		/// <param name="key">Key of the setting to retrieve.</param>
		/// <returns>The value of the setting or the default value if it is not found.</returns>
		public T GetValue<T>( string key )
		{
			lock( _syncRoot )
			{
				if( _settings.ContainsKey( key ) == true )
					return ( T )_settings[ key ];
				else
					return default( T );
			}
		}

		/// <summary>
		/// Get a value for the setting with the given key.
		/// </summary>
		/// <typeparam name="T">Type of the setting.</typeparam>
		/// <param name="key">Key of the setting to retrieve.</param>
		/// <param name="defaultValue">Default value if the key is not found.</param>
		/// <returns>The value of the setting or the given default value if it is not found.</returns>
		public T GetValue<T>( string key, T defaultValue )
		{
			lock( _syncRoot )
			{
				if( _settings.ContainsKey( key ) == true )
					return ( T )_settings[ key ];
				else
					return defaultValue;
			}
		}

		/// <summary>
		/// Set a value for the setting with the given key.
		/// </summary>
		/// <typeparam name="T">Type of the setting.</typeparam>
		/// <param name="key">Key of the setting to add/update.</param>
		/// <param name="value">The new value of the setting.</param>
		public void SetValue<T>( string key, T value )
		{
			Type type = typeof( T );
			if( this.GetTypeString( type ) == null )
				throw new ArgumentException( "Type " + type.ToString() + " is not supported for serialization." );

			lock( _syncRoot )
			{
				if( _settings.ContainsKey( key ) == true )
					_settings[ key ] = value;
				else
					_settings.Add( key, value );
			}
		}

		/// <summary>
		/// Set a value for the setting with the given key only if the setting does not exist.
		/// </summary>
		/// <typeparam name="T">Type of the setting.</typeparam>
		/// <param name="key">Key of the setting to add.</param>
		/// <param name="value">The new value of the setting.</param>
		public void SetValueIfEmpty<T>( string key, T value )
		{
			Type type = typeof( T );
			if( this.GetTypeString( type ) == null )
				throw new ArgumentException( "Type " + type.ToString() + " is not supported for serialization." );

			lock( _syncRoot )
			{
				if( _settings.ContainsKey( key ) == false )
					_settings.Add( key, value );
			}
		}
		
		protected bool Load( Stream stream, int depth )
		{
			lock( _syncRoot )
			{
				_settings.Clear();

				XmlDocument doc = new XmlDocument();

				try
				{
					doc.Load( stream );
				}
				catch
				{
					return false;
				}

				foreach( XmlElement settingRoot in doc.SelectNodes( "/settings/setting" ) )
				{
					string key = settingRoot.Attributes[ "key" ].Value;
					if( settingRoot.Attributes[ "isNull" ] != null )
					{
						_settings.Add( key, null );
					}
					else
					{
						string typeString = settingRoot.Attributes[ "type" ].Value;
						Type type = this.GetTypeObject( typeString );
						if( type == null )
							throw new InvalidOperationException( "Unsupported type " + typeString + " was found in the settings xml." );
						object value = this.LoadObject( type, settingRoot.InnerText, depth );
						_settings.Add( key, value );
					}
				}

				return true;
			}
		}

		/// <summary>
		/// Load settings from the given stream.
		/// </summary>
		/// <param name="stream">Settings XML stream as created by the <see cref="Settings.Save"/> method.</param>
		/// <returns><c>true</c> if the stream was loaded successfully.</returns>
		public bool Load( Stream stream )
		{
			return this.Load( stream, MaximumDepth );
		}

		/// <summary>
		/// Load settings from the given file.
		/// </summary>
		/// <param name="fileName">Settings XML file as created by the <see cref="Settings.Save"/> method.</param>
		/// <returns><c>true</c> if the file was loaded successfully.</returns>
		public bool Load( string fileName )
		{
			try
			{
				return this.Load( File.OpenRead( fileName ) );
			}
			catch
			{
				return false;
			}
		}

		protected void Save( Stream stream, int depth )
		{
			lock( _syncRoot )
			{
				XmlWriterSettings writerSettings = new XmlWriterSettings();
				writerSettings.CloseOutput = true;
				writerSettings.Indent = true;
				writerSettings.IndentChars = "\t";

				XmlWriter writer = XmlTextWriter.Create( stream, writerSettings );
				writer.WriteStartDocument();
				writer.WriteStartElement( "settings" );

				foreach( KeyValuePair<string, object> setting in _settings )
				{
					writer.WriteStartElement( "setting" );
					writer.WriteAttributeString( "key", setting.Key );
					if( setting.Value == null )
						writer.WriteAttributeString( "isNull", true.ToString() );
					else
					{
						writer.WriteAttributeString( "type", this.GetTypeString( setting.Value.GetType() ) );
						bool cdata;
						string data = SaveObject( setting.Value.GetType(), setting.Value, depth, out cdata );
						if( cdata == true )
							writer.WriteCData( data );
						else
							writer.WriteString( data );
					}
					writer.WriteEndElement();
				}

				writer.WriteEndElement();
				writer.WriteEndDocument();
				writer.Close();
			}
		}

		/// <summary>
		/// Save the settings collection to the given stream.
		/// </summary>
		/// <param name="stream">Settings XML stream to write.</param>
		public void Save( Stream stream )
		{
			this.Save( stream, MaximumDepth );
		}

		/// <summary>
		/// Save the settings collection to the given file.
		/// </summary>
		/// <param name="fileName">Settings XML file to write.</param>
		public void Save( string fileName )
		{
			string dir = System.IO.Path.GetDirectoryName( fileName );
			if( dir.Length > 0 )
			{
				if( System.IO.Directory.Exists( dir ) == false )
					System.IO.Directory.CreateDirectory( dir );
			}

			this.Save( File.Open( fileName, FileMode.Create, FileAccess.Write ) );
		}

		protected string GetTypeString( Type type )
		{
			if( type == typeof( bool ) )
				return "boolean";
			else if( type == typeof( int ) )
				return "integer";
			else if( type == typeof( long ) )
				return "longInteger";
			else if( type == typeof( float ) )
				return "single";
			else if( type == typeof( string ) )
				return "string";
			else if( type.GetInterface( "Noxa.ISettingsType" ) != null )
				return type.AssemblyQualifiedName;
			else if( type == typeof( bool ).MakeArrayType() )
				return "boolean[]";
			else if( type == typeof( int ).MakeArrayType() )
				return "integer[]";
			else if( type == typeof( long ).MakeArrayType() )
				return "longInteger[]";
			else if( type == typeof( float ).MakeArrayType() )
				return "single[]";
			else if( type == typeof( string ).MakeArrayType() )
				return "string[]";
			else if( ( type.IsArray == true ) &&
				( type.GetElementType().GetInterface( "Noxa.ISettingsType" ) != null ) )
				return type.AssemblyQualifiedName;
			else
				return null;
		}

		protected Type GetTypeObject( string type )
		{
			string baseType = type.Replace( "[]", "" );

			Type realType = null;
			switch( baseType )
			{
				case "boolean":
					realType = typeof( bool );
					break;
				case "integer":
					realType = typeof( int );
					break;
				case "longInteger":
					realType = typeof( long );
					break;
				case "single":
					realType = typeof( float );
					break;
				case "string":
					realType = typeof( string );
					break;
				default:
					// Try to load and see if it implements ISettingsType
					Type t = Type.GetType( baseType );
					if( t != null )
					{
						if( t.GetInterface( "Noxa.ISettingsType" ) != null )
							realType = t;
					}
					break;
			}

			if( realType == null )
				return null;

			if( baseType == type )
				return realType;
			else
				return realType.MakeArrayType();
		}

		protected object LoadObject( Type type, string data, int depth )
		{
			if( type == typeof( bool ) )
				return bool.Parse( data );
			else if( type == typeof( int ) )
				return int.Parse( data );
			else if( type == typeof( long ) )
				return long.Parse( data );
			else if( type == typeof( float ) )
				return float.Parse( data );
			else if( type == typeof( string ) )
				return data;
			else if( type.GetInterface( "Noxa.ISettingsType" ) != null )
			{
				// Need a string, int, bool
				return Activator.CreateInstance( type, data, depth - 1, false );
			}
			else if( type == typeof( bool ).MakeArrayType() )
			{
				string[] values = data.Split( ',' );
				bool[] array = new bool[ values.Length ];
				for( int n = 0; n < values.Length; n++ )
					array[ n ] = bool.Parse( values[ n ] );
				return array;
			}
			else if( type == typeof( int ).MakeArrayType() )
			{
				string[] values = data.Split( ',' );
				int[] array = new int[ values.Length ];
				for( int n = 0; n < values.Length; n++ )
					array[ n ] = int.Parse( values[ n ] );
				return array;
			}
			else if( type == typeof( long ).MakeArrayType() )
			{
				string[] values = data.Split( ',' );
				long[] array = new long[ values.Length ];
				for( int n = 0; n < values.Length; n++ )
					array[ n ] = long.Parse( values[ n ] );
				return array;
			}
			else if( type == typeof( float ).MakeArrayType() )
			{
				string[] values = data.Split( ',' );
				float[] array = new float[ values.Length ];
				for( int n = 0; n < values.Length; n++ )
					array[ n ] = float.Parse( values[ n ] );
				return array;
			}
			else if( type == typeof( string ).MakeArrayType() )
			{
				string[] values = data.Split( new string[] { StringSeperator }, StringSplitOptions.None );
				string[] array = new string[ values.Length ];
				for( int n = 0; n < values.Length; n++ )
					array[ n ] = values[ n ];
				return array;
			}
			else if( ( type.IsArray == true ) &&
				( type.GetElementType().GetInterface( "Noxa.ISettingsType" ) != null ) )
			{
				string[] values = data.Split( new string[] { new string( SettingsTypeSeperator, depth - 1 ) }, StringSplitOptions.None );
				ISettingsType[] array = ( ISettingsType[] )Activator.CreateInstance( type, values.Length );
				Type elementType = type.GetElementType();
				for( int n = 0; n < values.Length; n++ )
					array[ n ] = ( ISettingsType )Activator.CreateInstance( elementType, values[ n ], depth - 1, false );
				return array;
			}
			else
			{
				throw new ArgumentException( "Type " + type.ToString() + " is not supported for deserialization." );
			}
		}

		protected const string StringSeperator = "Æ";
		protected const char SettingsTypeSeperator = 'Å';

		protected string SaveObject( Type type, object value, int depth, out bool cdata )
		{
			cdata = false;

			if( type == typeof( bool ) )
				return value.ToString();
			else if( ( type == typeof( int ) ) ||
				( type == typeof( long ) ) )
				return value.ToString();
			else if( type == typeof( float ) )
				return value.ToString();
			else if( type == typeof( string ) )
			{
				cdata = true;
				return value as string;
			}
			else if( type.GetInterface( "Noxa.ISettingsType" ) != null )
			{
				ISettingsType st = value as ISettingsType;
				cdata = true;
				return st.Serialize( depth - 1 );
			}
			else if( type == typeof( bool ).MakeArrayType() )
			{
				StringBuilder sb = new StringBuilder();
				bool[] array = ( bool[] )value;
				for( int n = 0; n < array.Length; n++ )
				{
					if( n == array.Length - 1 )
						sb.Append( array[ n ] );
					else
						sb.AppendFormat( "{0},", array[ n ] );
				}
				return sb.ToString();
			}
			else if( type == typeof( int ).MakeArrayType() )
			{
				StringBuilder sb = new StringBuilder();
				int[] array = ( int[] )value;
				for( int n = 0; n < array.Length; n++ )
				{
					if( n == array.Length - 1 )
						sb.Append( array[ n ] );
					else
						sb.AppendFormat( "{0},", array[ n ] );
				}
				return sb.ToString();
			}
			else if( type == typeof( long ).MakeArrayType() )
			{
				StringBuilder sb = new StringBuilder();
				long[] array = ( long[] )value;
				for( int n = 0; n < array.Length; n++ )
				{
					if( n == array.Length - 1 )
						sb.Append( array[ n ] );
					else
						sb.AppendFormat( "{0},", array[ n ] );
				}
				return sb.ToString();
			}
			else if( type == typeof( float ).MakeArrayType() )
			{
				StringBuilder sb = new StringBuilder();
				float[] array = ( float[] )value;
				for( int n = 0; n < array.Length; n++ )
				{
					if( n == array.Length - 1 )
						sb.Append( array[ n ] );
					else
						sb.AppendFormat( "{0},", array[ n ] );
				}
				return sb.ToString();
			}
			else if( type == typeof( string ).MakeArrayType() )
			{
				StringBuilder sb = new StringBuilder();
				string[] array = ( string[] )value;
				for( int n = 0; n < array.Length; n++ )
				{
					if( n == array.Length - 1 )
						sb.Append( array[ n ] );
					else
						sb.AppendFormat( "{0}{1}", array[ n ], StringSeperator );
				}
				return sb.ToString();
			}
			else if( ( type.IsArray == true ) &&
				( type.GetElementType().GetInterface( "Noxa.ISettingsType" ) != null ) )
			{
				StringBuilder sb = new StringBuilder();
				cdata = true;
				ISettingsType[] array = ( ISettingsType[] )value;
				for( int n = 0; n < array.Length; n++ )
				{
					string data = array[ n ].Serialize( depth - 1 );
					if( n == array.Length - 1 )
						sb.Append( data );
					else
						sb.AppendFormat( "{0}{1}", data, new string( SettingsTypeSeperator, depth - 1 ) );
				}
				return sb.ToString();
			}
			else
			{
				throw new ArgumentException( "Type " + type.ToString() + " is not supported for serialization." );
			}
		}

		#region ISettingsType Members

		public Settings( string source, int depth, bool b )
		{
			byte[] buffer = Encoding.UTF8.GetBytes( source );
			using( MemoryStream stream = new MemoryStream( buffer, false ) )
				this.Load( stream, depth );
		}

		public string Serialize( int depth )
		{
			string ret;
			using( MemoryStream stream = new MemoryStream( 10000 ) )
			{
				this.Save( stream, depth );
				ret = Encoding.UTF8.GetString( stream.ToArray() );
			}
			return ret;
		}

		#endregion
	}
}
