﻿using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;
using System.Xml.Linq;

namespace Barotrauma
{
    public partial class Sprite
    {
        public static IEnumerable<Sprite> LoadedSprites
        {
            get { return list; }
        }

        private static HashSet<Sprite> list = new HashSet<Sprite>();

        //the file from which the texture is loaded
        //if two sprites use the same file, they share the same texture
        private string file;

        /// <summary>
        /// Reference to the xml element from where the sprite was created. Can be null if the sprite was not defined in xml!
        /// </summary>
        public XElement SourceElement { get; private set; }

        //the area in the texture that is supposed to be drawn
        private Rectangle sourceRect;

        //the offset used when drawing the sprite
        protected Vector2 offset;

        protected Vector2 origin;

        //the size of the drawn sprite, if larger than the source,
        //the sprite is tiled to fill the target size
        public Vector2 size;

        public float rotation;

        public SpriteEffects effects = SpriteEffects.None;

        protected float depth;

        public Rectangle SourceRect
        {
            get { return sourceRect; }
            set { sourceRect = value; }
        }

        public float Depth
        {
            get { return depth; }
            set { depth = MathHelper.Clamp(value, 0.001f, 0.999f); }
        }

        public Vector2 Origin
        {
            get { return origin; }
            set { origin = value; }
        }
        
        public string FilePath
        {
            get { return file; }
        }

        public override string ToString()
        {
            return FilePath + ": " + sourceRect;
        }

        partial void LoadTexture(ref Vector4 sourceVector, ref bool shouldReturn, bool premultiplyAlpha = true);
        partial void CalculateSourceRect();

        // TODO: use the Init method below?
        public Sprite(XElement element, string path = "", string file = "")
        {
            this.SourceElement = element;
            if (file == "")
            {
                file = element.GetAttributeString("texture", "");
            }
            
            if (file == "")
            {
                DebugConsole.ThrowError("Sprite " + element + " doesn't have a texture specified!");
                return;
            }

            if (!string.IsNullOrEmpty(path))
            {
                if (!path.EndsWith("/")) path += "/";
            }

            this.file = path + file;
            
            Vector4 sourceVector = element.GetAttributeVector4("sourcerect", Vector4.Zero);

            bool shouldReturn = false;
            LoadTexture(ref sourceVector, ref shouldReturn);
            if (shouldReturn) return;

            sourceRect = new Rectangle(
                (int)sourceVector.X, (int)sourceVector.Y,
                (int)sourceVector.Z, (int)sourceVector.W);

            origin = element.GetAttributeVector2("origin", new Vector2(0.5f, 0.5f));
            origin.X = origin.X * sourceRect.Width;
            origin.Y = origin.Y * sourceRect.Height;

            size = element.GetAttributeVector2("size", Vector2.One);
            size.X *= sourceRect.Width;
            size.Y *= sourceRect.Height;

            Depth = element.GetAttributeFloat("depth", 0.001f);

            list.Add(this);
        }

        internal void LoadParams(SpriteParams spriteParams, bool isFlipped)
        {
            SourceElement = spriteParams.Element;
            sourceRect = spriteParams.SourceRect;
            origin = spriteParams.Origin;
            origin.X = origin.X * sourceRect.Width;
            if (isFlipped)
            {
                origin.X = sourceRect.Width - origin.X;
            }
            origin.Y = origin.Y * sourceRect.Height;
            depth = spriteParams.Depth;
            // TODO: size?
        }

        public Sprite(string newFile, Vector2 newOrigin, bool preMultiplyAlpha = true)
        {
            Init(newFile, newOrigin: newOrigin, preMultiplyAlpha: preMultiplyAlpha);
        }
        
        public Sprite(string newFile, Rectangle? sourceRectangle, Vector2? newOffset = null, float newRotation = 0, bool preMultiplyAlpha = true)
        {
            Init(newFile, sourceRectangle: sourceRectangle, newOffset: newOffset, newRotation: newRotation, preMultiplyAlpha: preMultiplyAlpha);
        }
        
        private void Init(string newFile, Rectangle? sourceRectangle = null, Vector2? newOrigin = null, Vector2? newOffset = null, float newRotation = 0, 
            bool preMultiplyAlpha = true)
        {
            file = newFile;
            Vector4 sourceVector = Vector4.Zero;
            bool shouldReturn = false;
            LoadTexture(ref sourceVector, ref shouldReturn, preMultiplyAlpha);
            if (shouldReturn) return;
            if (sourceRectangle.HasValue)
            {
                sourceRect = sourceRectangle.Value;
            }
            else
            {
                CalculateSourceRect();
            }
            offset = newOffset ?? Vector2.Zero;
            if (newOrigin.HasValue)
            {
                origin = new Vector2(sourceRect.Width * newOrigin.Value.X, sourceRect.Height * newOrigin.Value.Y);
            }
            size = new Vector2(sourceRect.Width, sourceRect.Height);
            rotation = newRotation;
            if (!list.Contains(this))
            {
                list.Add(this);
            }
        }
        
        public void Remove()
        {
            list.Remove(this);

            DisposeTexture();
        }

        partial void DisposeTexture();
    }
}

