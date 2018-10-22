﻿using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Xml.Linq;

namespace Barotrauma
{
    partial class StructurePrefab : MapEntityPrefab
    {
        private bool canSpriteFlipX, canSpriteFlipY;

        private float health;
        
        //default size
        private Vector2 size;
        
        //does the structure have a physics body
        [Serialize(false, false)]
        public bool Body
        {
            get;
            private set;
        }

        //rotation of the physics body in degrees
        [Serialize(0.0f, false)]
        public float BodyRotation
        {
            get;
            private set;
        }
        
        //in display units
        [Serialize(0.0f, false)]
        public float BodyWidth
        {
            get;
            private set;
        }

        //in display units
        [Serialize(0.0f, false)]
        public float BodyHeight
        {
            get;
            private set;
        }

        [Serialize(false, false)]
        public bool Platform
        {
            get;
            private set;
        }

        [Serialize(false, false)]
        public bool AllowAttachItems
        {
            get;
            private set;
        }

        [Serialize(100.0f, false)]
        public float Health
        {
            get { return health; }
            set { health = Math.Max(value, 0.0f); }
        }

        [Serialize(false, false)]
        public bool CastShadow
        {
            get;
            private set;
        }

        [Serialize(Direction.None, false)]
        public Direction StairDirection
        {
            get;
            private set;
        }

        public bool CanSpriteFlipX
        {
            get { return canSpriteFlipX; }
        }

        public bool CanSpriteFlipY
        {
            get { return canSpriteFlipY; }
        }

        [Serialize("0,0", true)]
        public Vector2 Size
        {
            get { return size; }
            private set { size = value; }
        }

        public Sprite BackgroundSprite
        {
            get;
            private set;
        }
        
        public static void LoadAll(IEnumerable<string> filePaths)
        {            
            foreach (string filePath in filePaths)
            {
                XDocument doc = XMLExtensions.TryLoadXml(filePath);
                if (doc == null || doc.Root == null) return;

                foreach (XElement el in doc.Root.Elements())
                {        
                    StructurePrefab sp = Load(el);
                    
                    List.Add(sp);
                }
            }
        }
        
        public static StructurePrefab Load(XElement element)
        {
            StructurePrefab sp = new StructurePrefab
            {
                name = element.GetAttributeString("name", "")
            };
            if (string.IsNullOrEmpty(sp.name)) sp.name = element.Name.ToString();
            sp.identifier = element.GetAttributeString("identifier", "");

            string translatedName = TextManager.Get("EntityName." + sp.identifier, true);
            if (!string.IsNullOrEmpty(translatedName)) sp.name = translatedName;

            sp.Tags = new HashSet<string>();
            string joinedTags = element.GetAttributeString("tags", "");
            if (string.IsNullOrEmpty(joinedTags)) joinedTags = element.GetAttributeString("Tags", "");
            foreach (string tag in joinedTags.Split(','))
            {
                sp.Tags.Add(tag.Trim().ToLowerInvariant());
            }

            foreach (XElement subElement in element.Elements())
            {
                switch (subElement.Name.ToString())
                {
                    case "sprite":
                        sp.sprite = new Sprite(subElement);
                        if (subElement.Attribute("sourcerect") == null)
                        {
                            DebugConsole.ThrowError("Warning - sprite sourcerect not configured for structure \"" + sp.name + "\"!");
                        }

                        if (subElement.GetAttributeBool("fliphorizontal", false)) 
                            sp.sprite.effects = SpriteEffects.FlipHorizontally;
                        if (subElement.GetAttributeBool("flipvertical", false)) 
                            sp.sprite.effects = SpriteEffects.FlipVertically;
                        
                        sp.canSpriteFlipX = subElement.GetAttributeBool("canflipx", true);
                        sp.canSpriteFlipY = subElement.GetAttributeBool("canflipy", true);

                        break;
                    case "specularsprite":
                        sp.specularSprite = new Sprite(subElement);
                        break;
                    case "backgroundsprite":
                        sp.BackgroundSprite = new Sprite(subElement);

                        if (subElement.GetAttributeBool("fliphorizontal", false)) 
                            sp.BackgroundSprite.effects = SpriteEffects.FlipHorizontally;
                        if (subElement.GetAttributeBool("flipvertical", false)) 
                            sp.BackgroundSprite.effects = SpriteEffects.FlipVertically;

                        break;
                }
            }

            if (!Enum.TryParse(element.GetAttributeString("category", "Structure"), true, out MapEntityCategory category))
            {
                category = MapEntityCategory.Structure;
            }
            sp.Category = category;

            string aliases = element.GetAttributeString("aliases", "");
            if (!string.IsNullOrWhiteSpace(aliases))
            {
                sp.Aliases = aliases.Split(',');
            }

            SerializableProperty.DeserializeProperties(sp, element);
            string translatedDescription = TextManager.Get("EntityDescription." + sp.identifier, true);
            if (!string.IsNullOrEmpty(translatedDescription)) sp.Description = translatedDescription;

            //backwards compatibility
            if (element.Attribute("size") == null)
            {
                sp.size = Vector2.Zero;
                sp.size.X = element.GetAttributeFloat("width", 0.0f);
                sp.size.Y = element.GetAttributeFloat("height", 0.0f);
            }

            if (!category.HasFlag(MapEntityCategory.Legacy) && string.IsNullOrEmpty(sp.identifier))
            {
                DebugConsole.ThrowError(
                    "Structure prefab \"" + sp.name + "\" has no identifier. All structure prefabs have a unique identifier string that's used to differentiate between items during saving and loading.");
            }
            if (!string.IsNullOrEmpty(sp.identifier))
            {
                MapEntityPrefab existingPrefab = List.Find(e => e.Identifier == sp.identifier);
                if (existingPrefab != null)
                {
                    DebugConsole.ThrowError(
                        "Map entity prefabs \"" + sp.name + "\" and \"" + existingPrefab.Name + "\" have the same identifier!");
                }
            }

            return sp;
        }

        public override void UpdatePlacing(Camera cam)
        {
            Vector2 position = Submarine.MouseToWorldGrid(cam, Submarine.MainSub);
            Rectangle newRect = new Rectangle((int)position.X, (int)position.Y, (int)size.X, (int)size.Y);
            
            if (placePosition == Vector2.Zero)
            {
                if (PlayerInput.LeftButtonHeld())
                    placePosition = Submarine.MouseToWorldGrid(cam, Submarine.MainSub);

                newRect.X = (int)position.X;
                newRect.Y = (int)position.Y;
            }
            else
            {
                Vector2 placeSize = size;
                if (ResizeHorizontal) placeSize.X = position.X - placePosition.X;
                if (ResizeVertical) placeSize.Y = placePosition.Y - position.Y;

                newRect = Submarine.AbsRect(placePosition, placeSize);

                if (PlayerInput.LeftButtonReleased())
                {
                    //don't allow resizing width/height to zero
                   if ((!ResizeHorizontal || placeSize.X != 0.0f) && (!ResizeVertical || placeSize.Y != 0.0f))
                    {
                        newRect.Location -= MathUtils.ToPoint(Submarine.MainSub.Position);

                        var structure = new Structure(newRect, this, Submarine.MainSub);
                        structure.Submarine = Submarine.MainSub;
                    }

                    selected = null;
                    return;
                }
            }
            
            if (PlayerInput.RightButtonHeld()) selected = null;
        }
    }
}
