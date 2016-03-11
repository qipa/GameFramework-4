﻿using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Creates a simple path-finding compatable graph of the world.
/// Each tile is a node. Each walkable neighbour from a tile is linked via edge connection.
/// </summary>
public class Path_TileGraph
{
    public Dictionary<Tile, Path_Node<Tile>> nodes;

    public Path_TileGraph(World world)
    {
        //Create nodes for each walkable floor tile. 
        //Non-floor tiles are not walkable currently, things like walls are non-walkable.
        nodes = new Dictionary<Tile, Path_Node<Tile>>();

        for (int x = 0; x < world.Width; x++)
        {
            for (int y = 0; y < world.Height; y++)
            {
                Tile t = world.GetTileAt(x, y);

                if (t.Type != TileType.Empty) //Ignore the bulk of the map - empty tiles.
                {
                    Path_Node<Tile> n = new Path_Node<Tile>();
                    n.data = t;
                    nodes.Add(t, n);
                }
            }
        }

        Debug.Log("Path_TileGraph: Created " + nodes.Count + "nodes.");

        //TODO Debug remove
        int edgeCount = 0;

        foreach (Tile t in nodes.Keys)
        {
            Path_Node<Tile> n = nodes[t];
            List<Path_Edge<Tile>> edges = new List<Path_Edge<Tile>>();


            //Get list of neighbours for tile.
            Tile[] neighbours = t.GetNeighbours(true); //may return null

            //If neighbour exists and is walkable, create edge to note.
            for (int i = 0; i < neighbours.Length; i++)
            {
                if (neighbours[i] != null && neighbours[i].movementCost > 0)
                {
                    Path_Edge<Tile> e = new Path_Edge<Tile>();
                    e.cost = neighbours[i].movementCost;
                    e.node = nodes[ neighbours[i] ];

                    //Add edge to list
                    edges.Add(e);

                    //TODO debug remove
                    edgeCount++;
                }
            }

            n.edges = edges.ToArray();
        }
        //TODO debug remove
        Debug.Log("Path_TileGraph: Created " + edgeCount + "edges.");

    } //end Path_TileGraph constructor


} //end Path_TileGraph Class