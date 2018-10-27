﻿using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;

namespace Barotrauma
{
    public class GUIImage : GUIComponent
    {
        public float Rotation;

        private Sprite sprite;

        private Rectangle sourceRect;

        private bool crop;

        private bool scaleToFit;
        
        public bool Crop
        {
            get
            { 
                return crop;
            }
            set
            {
                crop = value;
                if (crop)
                {                                
                    sourceRect.Width = Math.Min(sprite.SourceRect.Width, Rect.Width);
                    sourceRect.Height = Math.Min(sprite.SourceRect.Height, Rect.Height);
                }
            }
        }

        public float Scale
        {
            get;
            set;
        }

        public Rectangle SourceRect
        {
            get { return sourceRect; }
            set { sourceRect = value; }
        }

        public Sprite Sprite
        {
            get { return sprite; }
            set
            {
                if (sprite == value) return;
                sprite = value;
                sourceRect = sprite.SourceRect;
                if (scaleToFit) RecalculateScale();                
            }
        }

        public GUIImage(RectTransform rectT, string style)
            : this(rectT, null, null, false, style)
        {
        }

        public GUIImage(RectTransform rectT, Sprite sprite, Rectangle? sourceRect = null, bool scaleToFit = false) 
            : this(rectT, sprite, sourceRect, scaleToFit, null)
        {
        }

        private GUIImage(RectTransform rectT, Sprite sprite, Rectangle? sourceRect, bool scaleToFit, string style) : base(style, rectT)
        {
            this.scaleToFit = scaleToFit;
            Sprite = sprite;
            if (sourceRect.HasValue)
            {
                this.sourceRect = sourceRect.Value;
            }
            else
            {
                this.sourceRect = sprite == null ? Rectangle.Empty : sprite.SourceRect;
            }
            if (style == null)
            {
                color = Color.White;
                hoverColor = Color.White;
                selectedColor = Color.White;
            }
            if (!scaleToFit)
            {
                Scale = 1.0f;
            }
            else
            {
                rectT.SizeChanged += RecalculateScale;
            }
        }

        protected override void Draw(SpriteBatch spriteBatch)
        {
            if (!Visible) return;

            Color currColor = GetCurrentColor(state);

            if (style != null)
            {
                foreach (UISprite uiSprite in style.Sprites[state])
                {
                    if (Math.Abs(Rotation) > float.Epsilon)
                    {
                        float scale = Math.Min(Rect.Width / uiSprite.Sprite.size.X, Rect.Height / uiSprite.Sprite.size.Y);
                        spriteBatch.Draw(uiSprite.Sprite.Texture, Rect.Center.ToVector2(), uiSprite.Sprite.SourceRect, currColor * (currColor.A / 255.0f), Rotation, uiSprite.Sprite.size / 2,
                            Scale * scale, SpriteEffects.None, 0.0f);
                    }
                    else
                    {
                        uiSprite.Draw(spriteBatch, Rect, currColor * (currColor.A / 255.0f), SpriteEffects.None);
                    }
                }
            }
            else if (sprite?.Texture != null)
            {
                spriteBatch.Draw(sprite.Texture, Rect.Center.ToVector2(), sourceRect, currColor * (currColor.A / 255.0f), Rotation, sprite.size / 2,
                    Scale, SpriteEffects.None, 0.0f);
            }
        }

        private void RecalculateScale()
        {
            Scale = sprite.SourceRect.Width == 0 || sprite.SourceRect.Height == 0 ?
                1.0f :
                Math.Min(RectTransform.Rect.Width / (float)sprite.SourceRect.Width, RectTransform.Rect.Height / (float)sprite.SourceRect.Height);
        }
    }
}
