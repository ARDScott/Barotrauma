﻿using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;

namespace Barotrauma
{
    partial class WayPoint : MapEntity
    {
        private static Texture2D iconTexture;
        private const int IconSize = 32;
        private static int[] iconIndices = { 3, 0, 1, 2 };

        public override void Draw(SpriteBatch spriteBatch, bool editing, bool back = true, bool specular = false)
        {
            if (!editing && !GameMain.DebugDraw) return;

            if (IsHidden()) return;

            //Rectangle drawRect =
            //    Submarine == null ? rect : new Rectangle((int)(Submarine.DrawPosition.X + rect.X), (int)(Submarine.DrawPosition.Y + rect.Y), rect.Width, rect.Height);

            Vector2 drawPos = Position;
            if (Submarine != null) drawPos += Submarine.DrawPosition;
            drawPos.Y = -drawPos.Y;

            Color clr = currentHull == null ? Color.Blue : Color.White;
            if (IsSelected) clr = Color.Red;
            if (isHighlighted) clr = Color.DarkRed;

            int iconX = iconIndices[(int)spawnType] * IconSize % iconTexture.Width;
            int iconY = (int)(Math.Floor(iconIndices[(int)spawnType] * IconSize / (float)iconTexture.Width)) * IconSize;

            int iconSize = ConnectedGap == null && Ladders == null ? IconSize : (int)(IconSize * 1.5f);

            spriteBatch.Draw(iconTexture,
                new Rectangle((int)(drawPos.X - iconSize / 2), (int)(drawPos.Y - iconSize / 2), iconSize, iconSize),
                new Rectangle(iconX, iconY, IconSize, IconSize), clr);

            //GUI.DrawRectangle(spriteBatch, new Rectangle(drawRect.X, -drawRect.Y, rect.Width, rect.Height), clr, true);

            //GUI.SmallFont.DrawString(spriteBatch, Position.ToString(), new Vector2(Position.X, -Position.Y), Color.White);

            foreach (MapEntity e in linkedTo)
            {
                GUI.DrawLine(spriteBatch,
                    drawPos,
                    new Vector2(e.DrawPosition.X, -e.DrawPosition.Y),
                    Color.Green, width: 5);
            }
        }

        private bool IsHidden()
        {
            if (spawnType == SpawnType.Path)
            {
                return (!GameMain.DebugDraw && !ShowWayPoints);
            }
            else
            {
                return (!GameMain.DebugDraw && !ShowSpawnPoints);
            }
        }

        public override void UpdateEditing(Camera cam)
        {
            if (editingHUD == null || editingHUD.UserData != this)
            {
                editingHUD = CreateEditingHUD();
            }
            
            if (PlayerInput.LeftButtonClicked())
            {
                Vector2 position = cam.ScreenToWorld(PlayerInput.MousePosition);

                foreach (MapEntity e in mapEntityList)
                {
                    if (e.GetType() != typeof(WayPoint)) continue;
                    if (e == this) continue;

                    if (!Submarine.RectContains(e.Rect, position)) continue;

                    linkedTo.Add(e);
                    e.linkedTo.Add(this);
                }
            }
        }

        private bool ChangeSpawnType(GUIButton button, object obj)
        {
            GUITextBlock spawnTypeText = button.Parent.GetChildByUserData("spawntypetext") as GUITextBlock;

            spawnType += (int)button.UserData;

            if (spawnType > SpawnType.Cargo) spawnType = SpawnType.Human;
            if (spawnType < SpawnType.Human) spawnType = SpawnType.Cargo;

            spawnTypeText.Text = spawnType.ToString();

            return true;
        }

        private bool EnterIDCardDesc(GUITextBox textBox, string text)
        {
            IdCardDesc = text;
            textBox.Text = text;
            textBox.Color = Color.Green;

            textBox.Deselect();

            return true;
        }
        private bool EnterIDCardTags(GUITextBox textBox, string text)
        {
            IdCardTags = text.Split(',');
            textBox.Text = text;
            textBox.Color = Color.Green;

            textBox.Deselect();

            return true;
        }

        private bool EnterAssignedJob(GUITextBox textBox, string text)
        {
            string trimmedName = text.ToLowerInvariant().Trim();
            assignedJob = JobPrefab.List.Find(jp => jp.Name.ToLowerInvariant() == trimmedName);

            if (assignedJob != null && trimmedName != TextManager.Get("None").ToLowerInvariant())
            {
                textBox.Color = Color.Green;
                textBox.Text = (assignedJob == null) ? TextManager.Get("None") : assignedJob.Name;
            }

            textBox.Deselect();

            return true;
        }

        private bool TextBoxChanged(GUITextBox textBox, string text)
        {
            textBox.Color = Color.Red;

            return true;
        }

        private GUIComponent CreateEditingHUD(bool inGame = false)
        {
            int width = 500;
            int height = spawnType == SpawnType.Path ? 80 : 200;
            int x = GameMain.GraphicsWidth / 2 - width / 2, y = 30;

            editingHUD = new GUIFrame(new RectTransform(new Point(width, height), GUI.Canvas) { ScreenSpaceOffset = new Point(x, y) })
            {
                UserData = this
            };

            var paddedFrame = new GUILayoutGroup(new RectTransform(new Vector2(0.9f, 0.85f), editingHUD.RectTransform, Anchor.Center))
            {
                Stretch = true,
                RelativeSpacing = 0.05f
            };

            if (spawnType == SpawnType.Path)
            {
                new GUITextBlock(new RectTransform(new Vector2(1.0f, 0.2f), paddedFrame.RectTransform), TextManager.Get("Editing") + " " + TextManager.Get("Waypoint"));
                new GUITextBlock(new RectTransform(new Vector2(1.0f, 0.2f), paddedFrame.RectTransform), TextManager.Get("LinkWaypoint"));
            }
            else
            {
                new GUITextBlock(new RectTransform(new Vector2(1.0f, 0.2f), paddedFrame.RectTransform), TextManager.Get("Editing") + " " + TextManager.Get("Spawnpoint"));
                
                var spawnTypeContainer = new GUILayoutGroup(new RectTransform(new Vector2(1.0f, 0.2f), paddedFrame.RectTransform), isHorizontal: true)
                {
                    Stretch = true,
                    RelativeSpacing = 0.05f
                };
                new GUITextBlock(new RectTransform(new Vector2(0.5f, 1.0f), spawnTypeContainer.RectTransform), TextManager.Get("SpawnType") + ": ");

                var button = new GUIButton(new RectTransform(new Vector2(0.1f, 1.0f), spawnTypeContainer.RectTransform), "-")
                {
                    UserData = -1,
                    OnClicked = ChangeSpawnType
                };
                new GUITextBlock(new RectTransform(new Vector2(0.3f, 1.0f), spawnTypeContainer.RectTransform), spawnType.ToString(), textAlignment: Alignment.Center)
                {
                    UserData = "spawntypetext"
                };
                button = new GUIButton(new RectTransform(new Vector2(0.1f, 1.0f), spawnTypeContainer.RectTransform), "+")
                {
                    UserData = 1,
                    OnClicked = ChangeSpawnType
                };


                var descText = new GUITextBlock(new RectTransform(new Vector2(1.0f, 0.2f), paddedFrame.RectTransform), 
                    TextManager.Get("IDCardDescription"), font: GUI.SmallFont);
                GUITextBox propertyBox = new GUITextBox(new RectTransform(new Vector2(0.5f, 1.0f), descText.RectTransform, Anchor.CenterRight), idCardDesc)
                {
                    MaxTextLength = 150,
                    OnEnterPressed = EnterIDCardDesc,
                    OnTextChanged = TextBoxChanged,
                    ToolTip = TextManager.Get("IDCardDescriptionTooltip")
                };


                var tagsText = new GUITextBlock(new RectTransform(new Vector2(1.0f, 0.2f), paddedFrame.RectTransform),
                    TextManager.Get("IDCardTags"), font: GUI.SmallFont);
                propertyBox = new GUITextBox(new RectTransform(new Vector2(0.5f, 1.0f), tagsText.RectTransform, Anchor.CenterRight), string.Join(", ", idCardTags))
                {
                    MaxTextLength = 60,
                    OnEnterPressed = EnterIDCardTags,
                    OnTextChanged = TextBoxChanged,
                    ToolTip = TextManager.Get("IDCardTagsTooltip")
                };


                var jobsText = new GUITextBlock(new RectTransform(new Vector2(1.0f, 0.2f), paddedFrame.RectTransform),
                    TextManager.Get("SpawnpointJobs"), font: GUI.SmallFont);
                propertyBox = new GUITextBox(new RectTransform(new Vector2(0.5f, 1.0f), jobsText.RectTransform, Anchor.CenterRight), (assignedJob == null) ? "None" : assignedJob.Name)
                {
                    MaxTextLength = 60,
                    OnEnterPressed = EnterAssignedJob,
                    OnTextChanged = TextBoxChanged,
                    ToolTip = TextManager.Get("SpawnpointJobsTooltip")
                };
            }
            
            PositionEditingHUD();

            return editingHUD;
        }        
    }
}
