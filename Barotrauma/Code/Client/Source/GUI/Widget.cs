﻿using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Barotrauma.Extensions;

namespace Barotrauma
{
    class Widget
    {
        public enum Shape
        {
            Rectangle,
            Circle
        }

        public Shape shape;
        public string tooltip;
        public Rectangle DrawRect => new Rectangle((int)(DrawPos.X - (float)size / 2), (int)(DrawPos.Y - (float)size / 2), size, size);
        public Rectangle InputRect
        {
            get
            {
                var inputRect = DrawRect;
                inputRect.Inflate(inputAreaMargin.X, inputAreaMargin.Y);
                return inputRect;
            }
        }

        public Vector2 DrawPos { get; set; }
        public int size = 10;
        /// <summary>
        /// Used only for circles.
        /// </summary>
        public int sides = 40;
        /// <summary>
        /// Currently used only for rectangles.
        /// </summary>
        public bool isFilled;
        public Point inputAreaMargin;
        public Color color = Color.Red;
        public Color textColor = Color.White;
        public Color textBackgroundColor = Color.Black * 0.5f;
        public readonly string id;

        public event Action Selected;
        public event Action Deselected;
        public event Action Hovered;
        public event Action Clicked;
        public event Action MouseDown;
        public event Action MouseUp;
        public event Action<float> MouseHeld;
        public event Action<float> PreUpdate;
        public event Action<float> PostUpdate;
        public event Action<SpriteBatch, float> PreDraw;
        public event Action<SpriteBatch, float> PostDraw;

        public Action refresh;

        public bool IsSelected => enabled && selectedWidgets.Contains(this);
        public bool IsControlled => IsSelected && PlayerInput.LeftButtonHeld();
        public bool IsMouseOver => GUI.MouseOn == null && InputRect.Contains(PlayerInput.MousePosition);
        private bool enabled = true;
        public bool Enabled
        {
            get { return enabled; }
            set
            {
                enabled = value;
                if (selectedWidgets.Contains(this))
                {
                    selectedWidgets.Remove(this);
                }
            }
        }

        private static bool multiselect;
        public static bool EnableMultiSelect
        {
            get { return multiselect; }
            set
            {
                multiselect = value;
                if (!multiselect && selectedWidgets.Multiple())
                {
                    selectedWidgets = selectedWidgets.Take(1).ToList();
                }
            }
        }
        public Vector2? tooltipOffset;

        public Widget linkedWidget;

        public static List<Widget> selectedWidgets = new List<Widget>();

        public Widget(string id, int size, Shape shape)
        {
            this.id = id;
            this.size = size;
            this.shape = shape;
        }

        public virtual void Update(float deltaTime)
        {
            PreUpdate?.Invoke(deltaTime);
            if (!enabled) { return; }
            if (IsMouseOver)
            {
                Hovered?.Invoke();
                if ((multiselect && !selectedWidgets.Contains(this)) || selectedWidgets.None())
                {
                    selectedWidgets.Add(this);
                    Selected?.Invoke();
                }
            }
            else if (selectedWidgets.Contains(this))
            {
                selectedWidgets.Remove(this);
                Deselected?.Invoke();
            }
            if (IsSelected)
            {
                if (PlayerInput.LeftButtonHeld())
                {
                    MouseHeld?.Invoke(deltaTime);
                }
                if (PlayerInput.LeftButtonDown())
                {
                    MouseDown?.Invoke();
                }
                if (PlayerInput.LeftButtonReleased())
                {
                    MouseUp?.Invoke();
                }
                if (PlayerInput.LeftButtonClicked())
                {
                    Clicked?.Invoke();
                }
            }
            PostUpdate?.Invoke(deltaTime);
        }

        public virtual void Draw(SpriteBatch spriteBatch, float deltaTime)
        {
            PreDraw?.Invoke(spriteBatch, deltaTime);
            var drawRect = DrawRect;
            switch (shape)
            {
                case Shape.Rectangle:
                    GUI.DrawRectangle(spriteBatch, drawRect, color, isFilled, thickness: IsSelected ? 3 : 1);
                    break;
                case Shape.Circle:
                    ShapeExtensions.DrawCircle(spriteBatch, DrawPos, size / 2, sides, color, thickness: IsSelected ? 3 : 1);
                    break;
                default: throw new NotImplementedException(shape.ToString());
            }
            if (IsSelected)
            {
                if (!string.IsNullOrEmpty(tooltip))
                {
                    var offset = tooltipOffset ?? new Vector2(size, -size / 2);
                    GUI.DrawString(spriteBatch, DrawPos + offset, tooltip, textColor, textBackgroundColor);
                }
            }
            PostDraw?.Invoke(spriteBatch, deltaTime);
        }
    }
}
