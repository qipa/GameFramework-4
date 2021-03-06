﻿using UnityEngine;
using System.Collections.Generic;
using System;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;

// InstalledObjects are things like walls, doors, and furniture (e.g. a sofa)

public class Furniture : IXmlSerializable
{
    //Contains custom parameters for this piece of furniture. TODO LUA will be able to describe these.
    protected Dictionary<string, float> furnitureParameters;

    //These are called every tick. TODO - these will probably be the imported LUA code.
    protected Action<Furniture, float> updateActions;

    List<Job> jobs;

    //If furniture is worked by person - where is correct spot to stand relative to bottom left tile of furniture sprite
    public Vector2 jobSpotOffset = Vector2.zero;

    //Where to spawn items created by the job.
    public Vector2 jobSpawnSpotOffset = Vector2.zero;

    public Func<Furniture, Enterability> IsEnterable;

    public void tick(float deltaTime)
    {
        if (updateActions != null)
        {
            updateActions(this, deltaTime);
        }
    }

    // This represents the BASE tile of the object -- but in practice, large objects may actually occupy
    // multile tiles.
    public Tile tile {get; protected set;}

	// This "objectType" will be queried by the visual system to know what sprite to render for this object
	public string objectType {get; protected set;}

	// This is a multipler. So a value of "2" here, means you move twice as slowly (i.e. at half speed)
	// Tile types and other environmental effects may be combined.
	// For example, a "rough" tile (cost of 2) with a table (cost of 3) that is on fire (cost of 3)
	// would have a total movement cost of (2+3+3 = 8), so you'd move through this tile at 1/8th normal speed.
	// NOTE: If movementCost = 0, then this tile is impassible. (e.g. a wall).
	public float movementCost { get; protected set; } 

    public bool isRoomBorder { get; protected set; }

    // For example, a table might be 3x2
    public int Width { get; protected set; }
	public int Height { get; protected set; }

    public Color tint = Color.white; //Graphics in the model TODO

	public bool linksToNeighbour {get; protected set;}

    //TODO make get/set functions for furnitureParameters and call onchanged in the set rather than make this public.
    public Action<Furniture> cbOnChanged;
    public Action<Furniture> cbOnRemoved;

    Func<Tile, bool> funcPositionValidation;

	// TODO: Implement object rotation

    //public due to serializer reqs 
	public Furniture()
    {
        furnitureParameters = new Dictionary<string, float>();
        jobs = new List<Job>();

    }

    //Copy constructor - Clone() should be called instead of this. Direct calls will break sub-classing
    protected Furniture(Furniture other)
    {
        this.objectType = other.objectType;
        this.movementCost = other.movementCost;
        this.isRoomBorder = other.isRoomBorder;
        this.Width = other.Width;
        this.Height = other.Height;
        this.tint = other.tint;
        this.linksToNeighbour = other.linksToNeighbour;
        this.jobSpotOffset = other.jobSpotOffset;
        this.jobSpawnSpotOffset = other.jobSpawnSpotOffset;

        this.furnitureParameters = new Dictionary<string, float>(other.furnitureParameters);
        jobs = new List<Job>();

        if (other.updateActions != null)
        {
            this.updateActions = (Action<Furniture, float>)other.updateActions.Clone();
        }

        if (other.funcPositionValidation != null)
        {
            this.funcPositionValidation = (Func<Tile, bool>)other.funcPositionValidation.Clone();
        }

        this.IsEnterable = other.IsEnterable;
    }

    //Makes a copy of the current furniture object. Sub classes should override this.
    virtual public Furniture Clone()
    {
        return new Furniture(this);
    }

	public Furniture( string objectType, float movementCost = 1f, int width=1, int height=1, bool linksToNeighbour=false, bool isRoomBorder = false )
    {
		this.objectType = objectType;
        this.movementCost = movementCost;
        this.isRoomBorder = isRoomBorder;
        this.Width = width;
        this.Height = height;
        this.linksToNeighbour = linksToNeighbour;

        this.funcPositionValidation = this.__IsValidPosition;

        furnitureParameters = new Dictionary<string, float>();
    }

    static public Furniture PlaceInstance(Furniture proto, Tile tile)
    {
        if (proto.funcPositionValidation(tile) == false)
        {
            Debug.LogError("Furniture - PlaceInstance - Invalid Position.");
            return null;
        }

        Furniture obj = proto.Clone();
        obj.tile = tile;

        // FIXME: This assumes we are 1x1!
        if (tile.PlaceFurniture(obj) == false)
        {
            // For some reason, we weren't able to place our object in this tile.
            // (Probably it was already occupied.)

            // Do NOT return our newly instantiated object.
            // (It will be garbage collected.)
            return null;
        }

        if (obj.linksToNeighbour)
        {
            // This type of furniture links itself to its neighbours,
            // so we should inform our neighbours that they have a new
            // buddy.  Just trigger their OnChangedCallback.

            Tile t;
            int x = tile.X;
            int y = tile.Y;

            t = World.current.GetTileAt(x, y + 1);
            if (t != null && t.furniture != null && t.furniture.cbOnChanged != null && t.furniture.objectType == obj.objectType)
            {
                // We have a Northern Neighbour
                t.furniture.cbOnChanged(t.furniture);
            }
            t = World.current.GetTileAt(x + 1, y);
            if (t != null && t.furniture != null && t.furniture.cbOnChanged != null && t.furniture.objectType == obj.objectType)
            {
                // We have a Eastern Neighbour
                t.furniture.cbOnChanged(t.furniture);
            }
            t = World.current.GetTileAt(x, y - 1);
            if (t != null && t.furniture != null && t.furniture.cbOnChanged != null && t.furniture.objectType == obj.objectType)
            {
                // We have a Southern Neighbour
                t.furniture.cbOnChanged(t.furniture);
            }
            t = World.current.GetTileAt(x - 1, y);
            if (t != null && t.furniture != null && t.furniture.cbOnChanged != null && t.furniture.objectType == obj.objectType)
            {
                // We have a Western Neighbour
                t.furniture.cbOnChanged(t.furniture);
            }
        }//endif

        return obj;
    }//end PlaceInstance()

    public bool IsValidPosition(Tile t)
    {
        return funcPositionValidation(t);
    }

    //Returns true if object is able to be placed at x,y position.
    //TODO replace by validation checks farmed out to LUA files.
    private bool __IsValidPosition(Tile t)
    {
        if (t == null)
        {
            Debug.LogError("wtf uis his)");
        }

        //Loop for multi tile furniture
        for (int x_off = t.X; x_off < (t.X + Width); x_off++)
        {
            for (int y_off = t.Y; y_off < (t.Y + Height); y_off++)
            {
                Tile t2 = World.current.GetTileAt(x_off, y_off);

                if (t2.furniture != null)
                {
                    //Already something here
                    return false;
                }

                if (t2.Type != TileType.Floor)
                {
                    //invalid position
                    return false;
                }
            }
        }
        return true;
    }
   
    public float GetParameter(string key, float default_value = 0)
    {
        if (furnitureParameters.ContainsKey(key) == false)
        {
            return default_value;
        }

        return furnitureParameters[key];
    }

    public void SetParameter(string key, float value)
    {
        furnitureParameters[key] = value;
    }

    public void ChangeParameter(string key, float value)
    {
        if (furnitureParameters.ContainsKey(key) == false)
        {
            furnitureParameters[key] = value;
            return;
        }

        furnitureParameters[key] += value;
    }

    //Registers a function to be called every Tick. (Will be farmed out to LUA later) TODO
    public void RegisterUpdateAction(Action<Furniture, float> a)
    {
        updateActions += a;
    }
    
    public void UnRegisterUpdateAction(Action<Furniture, float> a)
    {
        updateActions -= a;
    }

    public int JobCount()
    {
        return jobs.Count;
    }

    public void AddJob(Job j)
    {
        j.furniture = this;
        jobs.Add(j);
        j.RegisterJobStoppedCallback(OnJobStopped);
        World.current.jobQueue.Enqueue(j, true);
    }

    void OnJobStopped(Job j)
    {
        RemoveJob(j);
    }

    protected void RemoveJob(Job j)
    {
        //j.CancelJob();
        j.UnregisterJobStoppedCallback(OnJobStopped);
        jobs.Remove(j);
        j.furniture = null;
        World.current.jobQueue.Remove(j);
    }

    protected void ClearJobs()
    {
        Job[] jobs_array = jobs.ToArray();
        foreach (Job j in jobs_array)
        {
            RemoveJob(j);
        }
    }

    public void CancelJobs()
    {
        Job[] jobs_array = jobs.ToArray();
        foreach (Job j in jobs_array)
        {
            j.CancelJob();
        }
    }

    public bool IsStockpile()
    {
        return objectType == "Stockpile";
    }

    public void Deconstruct()
    {

        tile.UnplaceFurninture();

        if (cbOnRemoved != null)
        {
            cbOnRemoved(this);
        }
        //All references to furniture should be gone now - GC should work.

        if (isRoomBorder)
        {
            Room.ReCalculateRoomsDelete(this.tile);
        }

        World.current.InvalidateTileGraph();

    }

    public Tile GetJobSpotTile()
    {
        return World.current.GetTileAt(tile.X + (int)jobSpotOffset.x, tile.Y + (int)jobSpotOffset.y);
    }

    public Tile GetSpawnSpotTile()
    {
        return World.current.GetTileAt(tile.X + (int)jobSpawnSpotOffset.x, tile.Y + (int)jobSpawnSpotOffset.y);
    }

    #region SaveLoadCode
    //For serializer - must be parameter-less

    public XmlSchema GetSchema()
    {
        return null;
    }

    public void WriteXml(XmlWriter writer)
    {
        writer.WriteAttributeString("X", tile.X.ToString());
        writer.WriteAttributeString("Y", tile.Y.ToString());
        writer.WriteAttributeString("objectType", objectType);

        foreach( string k in furnitureParameters.Keys)
        {
            writer.WriteStartElement("Param");
            writer.WriteAttributeString("name", k);
            writer.WriteAttributeString("value", furnitureParameters[k].ToString()); //This is a bit iffy, test this more later.

            writer.WriteEndElement();
        }
    }

    public void ReadXml(XmlReader reader)
    {
        //X, Y, and objectType should have already been set before this function is called.

        if (reader.ReadToDescendant("Param"))
        {
            do
            {
                string k = reader.GetAttribute("name");
                float v = float.Parse(reader.GetAttribute("value"));
                furnitureParameters[k] = v;
            } while (reader.ReadToNextSibling("Param"));
        }
    }

    #endregion

    #region callbacks
    public void RegisterOnChangedCallback(Action<Furniture> callbackFunc)
    {
        cbOnChanged += callbackFunc;
    }

    public void UnregisterOnChangedCallback(Action<Furniture> callbackFunc)
    {
        cbOnChanged -= callbackFunc;
    }

    public void RegisterOnRemovedCallback(Action<Furniture> callbackFunc)
    {
        cbOnRemoved += callbackFunc;
    }

    public void UnregisterOnRemovedCallback(Action<Furniture> callbackFunc)
    {
        cbOnRemoved -= callbackFunc;
    }
    #endregion
}
