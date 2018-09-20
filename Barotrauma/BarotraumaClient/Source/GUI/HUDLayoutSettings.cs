﻿using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;

namespace Barotrauma
{
    static class HUDLayoutSettings
    {
        public static bool DebugDraw;

        public static Rectangle ButtonAreaTop
        {
            get; private set;
        }

        public static Rectangle MessageAreaTop
        {
            get; private set;
        }

        public static Rectangle InventoryAreaUpper
        {
            get; private set;
        }

        public static Rectangle CrewArea
        {
            get; private set;
        }

        public static Rectangle ChatBoxArea
        {
            get; private set;
        }

        public static Alignment ChatBoxAlignment
        {
            get; private set;
        }

        public static Rectangle InventoryAreaLower
        {
            get; private set;
        }

        public static Rectangle HealthBarAreaLeft
        {
            get; private set;
        }
        public static Rectangle AfflictionAreaLeft
        {
            get; private set;
        }

        public static Rectangle HealthBarAreaRight
        {
            get; private set;
        }
        public static Rectangle AfflictionAreaRight
        {
            get; private set;
        }

        public static Rectangle HealthWindowAreaLeft
        {
            get; private set;
        }

        public static Rectangle HealthWindowAreaRight
        {
            get; private set;
        }

        public static Rectangle PortraitArea
        {
            get; private set;
        }

        public static int Padding
        {
            get; private set;
        }

        static HUDLayoutSettings()
        {
            GameMain.Instance.OnResolutionChanged += CreateAreas;
            GameMain.Config.OnHUDScaleChanged += CreateAreas;
            CreateAreas();
        }
        
        public static RectTransform ToRectTransform(Rectangle rect, RectTransform parent)
        {
            return new RectTransform(new Vector2(rect.Width / (float)GameMain.GraphicsWidth, rect.Height / (float)GameMain.GraphicsHeight), parent)
            {
                RelativeOffset = new Vector2(rect.X / (float)GameMain.GraphicsWidth, rect.Y / (float)GameMain.GraphicsHeight)
            };
        }

        public static void CreateAreas()
        {
            Padding = (int)(10 * GUI.Scale);

            //slice from the top of the screen for misc buttons (info, end round, server controls)
            ButtonAreaTop = new Rectangle(Padding, Padding, GameMain.GraphicsWidth - Padding * 2, (int)(50 * GUI.Scale));

            int crewAreaHeight = (int)Math.Max(GameMain.GraphicsHeight * 0.22f, 150);
            CrewArea = new Rectangle(Padding, ButtonAreaTop.Bottom + Padding, GameMain.GraphicsWidth - InventoryAreaUpper.Width - Padding * 3, crewAreaHeight);

            int portraitSize = (int)(GameMain.GraphicsHeight * 0.15f);
            PortraitArea = new Rectangle(GameMain.GraphicsWidth - portraitSize - Padding, GameMain.GraphicsHeight - portraitSize - Padding, portraitSize, portraitSize);

            //horizontal slices at the corners of the screen for health bar and affliction icons
            int healthBarWidth = (int)Math.Max(250 * GUI.Scale, 100);
            int healthBarHeight = (int)Math.Max(20 * GUI.Scale, 15);
            int afflictionAreaHeight = (int)(60 * GUI.Scale);
            HealthBarAreaLeft = new Rectangle(Padding, GameMain.GraphicsHeight - healthBarHeight - Padding, healthBarWidth, healthBarHeight);
            AfflictionAreaLeft = new Rectangle(Padding, HealthBarAreaLeft.Y - afflictionAreaHeight - Padding, healthBarWidth, afflictionAreaHeight);
            
            HealthBarAreaRight = new Rectangle(PortraitArea.X + Padding - healthBarWidth, PortraitArea.Y + Padding * 3, healthBarWidth, HealthBarAreaLeft.Height);
            AfflictionAreaRight = new Rectangle(HealthBarAreaRight.X, HealthBarAreaRight.Y - Padding - afflictionAreaHeight, healthBarWidth, afflictionAreaHeight);

            int messageAreaPos = GameMain.GraphicsWidth - HealthBarAreaRight.X;
            MessageAreaTop = new Rectangle(messageAreaPos + Padding, ButtonAreaTop.Bottom, GameMain.GraphicsWidth - (messageAreaPos + Padding) * 2, ButtonAreaTop.Height);

            //slice for the upper slots of the inventory (clothes, id card, headset)
            int inventoryAreaUpperWidth = (int)(GameMain.GraphicsWidth * 0.2f);
            int inventoryAreaUpperHeight = (int)(GameMain.GraphicsHeight * 0.2f);
            InventoryAreaUpper = new Rectangle(GameMain.GraphicsWidth - inventoryAreaUpperWidth - Padding, CrewArea.Y, inventoryAreaUpperWidth, inventoryAreaUpperHeight);

            //chatbox between upper and lower inventory areas, can be on either side depending on the alignment
            ChatBoxAlignment = Alignment.Right;
            int chatBoxWidth = (int)(500 * GUI.Scale);
            int chatBoxHeight = crewAreaHeight;
            ChatBoxArea = ChatBoxAlignment == Alignment.Left ?
                new Rectangle(Padding, CrewArea.Y, chatBoxWidth, chatBoxHeight) :
                new Rectangle(GameMain.GraphicsWidth - Padding - chatBoxWidth, CrewArea.Y, chatBoxWidth, chatBoxHeight);

            int lowerAreaHeight = (int)Math.Min(GameMain.GraphicsHeight * 0.25f, 280);
            InventoryAreaLower = new Rectangle(Padding, GameMain.GraphicsHeight - lowerAreaHeight, GameMain.GraphicsWidth - Padding * 2, lowerAreaHeight);

            int healthWindowY = CrewArea.Bottom + Padding;
            Rectangle healthWindowArea = ChatBoxAlignment == Alignment.Left ?
                new Rectangle(ChatBoxArea.Right + Padding, healthWindowY, GameMain.GraphicsWidth - ChatBoxArea.Width - inventoryAreaUpperWidth, GameMain.GraphicsHeight - healthWindowY - lowerAreaHeight / 2) :
                new Rectangle(Padding - ChatBoxArea.Width, healthWindowY, GameMain.GraphicsWidth - ChatBoxArea.Width - inventoryAreaUpperWidth, GameMain.GraphicsHeight - healthWindowY - lowerAreaHeight / 2);

            int healthWindowPadding = Padding * 3;
            HealthWindowAreaLeft = new Rectangle(healthWindowPadding, healthWindowY, GameMain.GraphicsWidth / 2 - healthWindowPadding, GameMain.GraphicsHeight - healthWindowY - lowerAreaHeight);
            HealthWindowAreaRight = new Rectangle(GameMain.GraphicsWidth / 2, healthWindowY, GameMain.GraphicsWidth / 2 - healthWindowPadding, GameMain.GraphicsHeight - healthWindowY - lowerAreaHeight);
            
        }

        public static void Draw(SpriteBatch spriteBatch)
        {
            GUI.DrawRectangle(spriteBatch, ButtonAreaTop, Color.White * 0.5f);
            GUI.DrawRectangle(spriteBatch, MessageAreaTop, Color.Orange * 0.5f);
            GUI.DrawRectangle(spriteBatch, InventoryAreaUpper, Color.Yellow * 0.5f);
            GUI.DrawRectangle(spriteBatch, CrewArea, Color.Blue * 0.5f);
            GUI.DrawRectangle(spriteBatch, ChatBoxArea, Color.Cyan * 0.5f);
            GUI.DrawRectangle(spriteBatch, HealthBarAreaLeft, Color.Red * 0.5f);
            GUI.DrawRectangle(spriteBatch, AfflictionAreaLeft, Color.Red * 0.5f);
            GUI.DrawRectangle(spriteBatch, HealthBarAreaRight, Color.Red * 0.5f);
            GUI.DrawRectangle(spriteBatch, AfflictionAreaRight, Color.Red * 0.5f);
            GUI.DrawRectangle(spriteBatch, InventoryAreaLower, Color.Yellow * 0.5f);
            GUI.DrawRectangle(spriteBatch, HealthWindowAreaLeft, Color.Red * 0.5f);
            GUI.DrawRectangle(spriteBatch, HealthWindowAreaRight, Color.Red * 0.5f);
        }
    }
}
