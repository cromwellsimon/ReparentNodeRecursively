using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ReparentNodeRecursively;

public static class GodotStatics
{
	/// <summary>
	/// This will return the Node and all of its nested children
	/// </summary>
	public static IEnumerable<Node> Enumerate(this Node inNode)
	{
		Queue<Node> untouchedNodes = new();
		untouchedNodes.Enqueue(inNode);
		while (untouchedNodes.Count > 0)
		{
			Node touchedNode = untouchedNodes.Dequeue();
			yield return touchedNode;
			foreach (Node child in touchedNode.GetChildren())
			{
				untouchedNodes.Enqueue(child);
			}
		}
	}

	public static bool IsAChildOf(this Node inNode, Node otherNode) => inNode.GetPathTo(otherNode).ToString().EndsWith("..") ? true : false;

	public static T? As<T>(this object inObject) where T : class => inObject is T ? (T)inObject : default;

	public static IEnumerable<string> GetAllFiles(string inPath = "res://")
	{
		Queue<string> untouchedDirectories = new();
		untouchedDirectories.Enqueue(inPath);
		while (untouchedDirectories.Count > 0)
		{
			string touchedDirectory = untouchedDirectories.Dequeue();
			foreach (string file in DirAccess.GetFilesAt(touchedDirectory))
			{
				yield return $"{touchedDirectory}/{file}";
			}
			foreach (string directory in DirAccess.GetDirectoriesAt(touchedDirectory))
			{
				if (directory.StartsWith('.') == false)
				{
					// Folders that start with a period are ignored by Godot. Files are not.
					untouchedDirectories.Enqueue(directory);
				}
			}
		}
	}

	public static IEnumerable<string> GetAllFilesOfExtension(string extension, string inPath = "res://")
	{
		return GetAllFiles(inPath).AsParallel().Where((file) => file.EndsWith(extension));
	}
}
