﻿using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Barotrauma
{
    public class GUIProgressBar : GUIComponent
    {
        private bool isHorizontal;

        private GUIFrame frame, slider;
        private float barSize;
                
        public delegate float ProgressGetterHandler();
        public ProgressGetterHandler ProgressGetter;

        public bool IsHorizontal
        {
            get { return isHorizontal; }
            set { isHorizontal = value; }
        }

        public float BarSize
        {
            get { return barSize; }
            set
            {
                barSize = MathHelper.Clamp(value, 0.0f, 1.0f);
                //UpdateRect();
            }
        }
        
        public GUIProgressBar(RectTransform rectT, float barSize, Color? color = null, string style = "") : base(style, rectT)
        {
            if (color.HasValue)
            {
                this.color = color.Value;
            }
            isHorizontal = (Rect.Width > Rect.Height);
            frame = new GUIFrame(new RectTransform(Vector2.One, rectT));
            GUI.Style.Apply(frame, "", this);
            slider = new GUIFrame(new RectTransform(Vector2.One, rectT));
            GUI.Style.Apply(slider, "Slider", this);
            this.barSize = barSize;
        }

        protected override void Draw(SpriteBatch spriteBatch)
        {
            if (!Visible) return;

            if (ProgressGetter != null) BarSize = ProgressGetter();   

            Rectangle sliderRect = new Rectangle(
                    frame.Rect.X,
                    (int)(frame.Rect.Y + (isHorizontal ? 0 : frame.Rect.Height * (1.0f - barSize))),
                    isHorizontal ? (int)((frame.Rect.Width) * barSize) : frame.Rect.Width,
                    isHorizontal ? (int)(frame.Rect.Height) : (int)(frame.Rect.Height * barSize));
            
            frame.Visible = true;
            slider.Visible = true;
            if (AutoDraw)
            {
                frame.DrawAuto(spriteBatch);
            }
            else
            {
                frame.DrawManually(spriteBatch);
            }

            Rectangle prevScissorRect = spriteBatch.GraphicsDevice.ScissorRectangle;
            if (BarSize <= 1.0f)
            {
                spriteBatch.End();
                spriteBatch.GraphicsDevice.ScissorRectangle = Rectangle.Intersect(prevScissorRect, sliderRect);
                spriteBatch.Begin(SpriteSortMode.Deferred, rasterizerState: GameMain.ScissorTestEnable);
            }

            Color currColor = GetCurrentColor(state);

            slider.Color = currColor;
            if (AutoDraw)
            {
                slider.DrawAuto(spriteBatch);
            }
            else
            {
                slider.DrawManually(spriteBatch);
            }
            //hide the slider, we've already drawn it manually
            frame.Visible = false;
            slider.Visible = false;
            if (BarSize <= 1.0f)
            {
                spriteBatch.End();
                spriteBatch.Begin(SpriteSortMode.Deferred, rasterizerState: GameMain.ScissorTestEnable);
                spriteBatch.GraphicsDevice.ScissorRectangle = prevScissorRect;
            }
        }
    }
}
