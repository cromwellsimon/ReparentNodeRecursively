using Godot;
using Godot.Collections;
using System.Text;
using static ReparentNodeRecursively.ReparentNodeRecursivelyHelpers;
using FileAccess = Godot.FileAccess;

namespace ReparentNodeRecursively;

// I made this before I enabled nullability in the project due to a bug with Godot... Given I'm not actually using this anymore, I don't feel a need to go back through it to add nullability.
#nullable disable
public partial class ReparentNodeRecursively : Control
{
	public class FileData
	{
		public required string FilePath { get; init; }
		public required string OldFileContent { get; init; }
#nullable enable
		public string? NewFileContent { get; set; }
#nullable disable
	}

	public LineEdit RootNodeNameEdit { get; private set; } = default!;
	public LineEdit OldPathEdit { get; private set; } = default!;
	public LineEdit NewParentPathEdit { get; private set; } = default!;
	public Button ReparentButton { get; private set; } = default!;
	public Label ProgressLabel { get; private set; } = default!;

	private PackedScene rootScene = null;

	// Very menial UI building so this can be built purely through code
	public override void _EnterTree()
	{
		SetAnchorsPreset(LayoutPreset.FullRect);

		Panel panel = new() { Name = nameof(Panel) };
		AddChild(panel);
		panel.SetAnchorsPreset(LayoutPreset.FullRect);

		MarginContainer marginContainer = new() { Name = nameof(MarginContainer) };
		AddChild(marginContainer);
		marginContainer.SetAnchorsPreset(LayoutPreset.FullRect);

		VBoxContainer vBoxContainer = new() { Name = nameof(VBoxContainer) };
		marginContainer.AddChild(vBoxContainer);

		HBoxContainer rootPackedScenePath = new();
		vBoxContainer.AddChild(rootPackedScenePath);

		rootPackedScenePath.AddChild(new Label() { Text = "Root Node Name: " });
		RootNodeNameEdit = new() { Name = nameof(RootNodeNameEdit), SizeFlagsHorizontal = SizeFlags.Expand };
		rootPackedScenePath.AddChild(RootNodeNameEdit);

		HBoxContainer oldParentPath = new();
		vBoxContainer.AddChild(oldParentPath);

		oldParentPath.AddChild(new Label() { Text = "Old Path: " });
		OldPathEdit = new() { Name = nameof(OldPathEdit), SizeFlagsHorizontal = SizeFlags.Expand };
		oldParentPath.AddChild(OldPathEdit);

		HBoxContainer newParentPath = new();
		vBoxContainer.AddChild(newParentPath);

		newParentPath.AddChild(new Label() { Text = "New Parent Path: " });
		NewParentPathEdit = new() { Name = nameof(NewParentPathEdit), SizeFlagsHorizontal = SizeFlags.Expand };
		newParentPath.AddChild(NewParentPathEdit);

		ReparentButton = new() { Name = nameof(ReparentButton), Text = "Reparent", SizeFlagsHorizontal = SizeFlags.ShrinkCenter };
		vBoxContainer.AddChild(ReparentButton);

		ProgressLabel = new() { Name = nameof(ProgressLabel) };
		vBoxContainer.AddChild(ProgressLabel);
	}

	public override void _Ready()
	{
		ReparentButton.Pressed += ReparentButton_Pressed;
	}

	private void ReparentButton_Pressed()
	{
		// There's a few different ways we can handle replacing relative NodePaths...

		// The simplest but also most fragile solution would be to simply count the number of '/' keys that happen before-and-after the change in the NodePath
		// and either add or remove "../" from any relative NodePaths based off of that

		// The second potential solution (the one I'm going to do) is that before we replace all of the .tscn files with the Regex, open every Scene that contains the Node,
		// and place a dummy Node (with the exact same name) in the new position. Convert all NodePaths that pointed to and from the original Node
		// (which I have access to with the _Bundled variable in a PackedScene... (EDIT: that's wrong, I don't lol)) to instead point to and from the dummy Node and use that to update the NodePaths.

		// As some ideas for other things that I could do (I don't have any intent on doing them now but they are realistically viable):
		// Intelligent [Export] Variable renames and Intelligent Scene Node renames.

		// For Intelligent Variable renames, I could open every script and look through every Resource and Node, get every Script, see if it matches the Script you passed in.
		// If it does, look for that Node or Resource on the .tscn and attempt a Regex.Replace on the old variable name with the new value you give it.
		// If nothing gets replaced, that means that the variable was not overridden so there was nothing to be replaced. Furthermore, this won't replace any variables for other Scripts with the same variable name.
		// EDIT: This actually wouldn't work as I'd expect it to because it wouldn't be able to effectively deal with inheritance or interfaces

		// For Intelligent Scene Node renames, it would work essentially like how this ReparentNodeRecursively does now (including the whole thing with the dummy Node).
		// The biggest difference is that the dummy Node will have the new name that you give it. There are some other differences but that's the main thing.
		// EDIT: This actually wouldn't work as I'd expect it to for the same reason as above: Scene inheritance makes the base file different from what I'd want it to be and it seems like,
		// unless if I'm recursively checking, say, in HandgunBullets.tscn where the root is: [node name="HandgunBullets" instance=ExtResource("4_slmot")], looking at the instance ID,
		// looking at the path that it belongs to, see if that's equal, rinse-and-repeat, then that could potentially work. So, unlike the Intelligent Variable renames, this is actually plausible
		// but it's just easier to do a simple find-and-replace assuming that I have the ID-suffixed names.
		// The reason why this works as-is is because it relies on having the ID-suffixed names; it's intelligent in every regard except for that. And, in fairness, that's essentially how it works for every other engine I think.

		// But, for my current workflow, I'm perfectly fine with having the ID-suffixed names. This is the only problem that I have which the ID-suffixed names aren't able to fix.

		//rootScene = ResourceLoader.Load<PackedScene>(RootScenePathEdit.Text);
		//rootScene._Bundled

		ProgressLabel.Text = "Searching...";

		Array<string> tscnFilePaths = new(GodotStatics.GetAllFilesOfExtension(".tscn"));

		string rootNodeName = RootNodeNameEdit.Text;
		string oldPath = OldPathEdit.Text;
		string oldParentPath = oldPath.Split('/')[0..^1].Join("/");
		string targetNodeName = oldPath.Split('/').LastOrDefault();
		string newParentPath = NewParentPathEdit.Text;
		string newParentName = newParentPath.Split("/").LastOrDefault();
		//Regex reparentNodeRegex = new($@"(?<RestOfContent>name=""{targetNodeName}"" .*parent=""([^""]*?\/?)){newParentPath}[^""]*", RegexOptions.Compiled);
		//Regex findElementsWithNodePathsRegex = new($"^\\[(.*)\\]\\n(?:.*\\n)*.*NodePath\\(\"(.*)\"\\)\r\n", RegexOptions.Compiled);

		ProgressLabel.Text = "Updating...";


		// So I don't have to read the file again, simply store it as a string in this Dictionary. Surely I won't run out of RAM...
		System.Collections.Generic.Dictionary<FileData, List<RetargetValueMap>> contentToReplace = new();

		// I wish I could do this part in parallel but this gives a boatload of errors if it's not on the main thread lol
		foreach (string filePath in tscnFilePaths)
		{
			using (FileAccess readFile = FileAccess.Open(filePath, FileAccess.ModeFlags.Read))
			{
				string fileAsText = readFile.GetAsText();

				// Doesn't matter where in the file it is, if that name is found, then it's considered a candidate for this because that means
				// it is either being referenced as a Parent to a child, it or a child is being referenced as a NodePath to anything else in the scene, or that values on it have been altered.
				if (/*filePath == "res://Prefabs/Actions/Door/WarpToNodePrefab.tscn" && */fileAsText.Contains(targetNodeName))
				{
					FileData fileData = new() { FilePath = filePath, OldFileContent = fileAsText };
					contentToReplace.Add(fileData, new());
					Node scene = ResourceLoader.Load<PackedScene>(filePath).Instantiate();

					Array<Node> foundTscnNodes = new(scene.FindChildren(rootNodeName).Select((rootNode) => { return rootNode.GetParent(); }));

					List<RetargetValueMap> retargetValuesMap = new();

					foreach (Node tscnNode in foundTscnNodes)
					{
						Node targetNode = tscnNode.GetNode($"{oldPath}");
						Node newParent = tscnNode.GetNode($"{newParentPath}");

						IDictionary<Node, Node> remappedNodes = targetNode.CopyNodeStructure(newParent);


						foreach (KeyValuePair<Node, Node> pair in remappedNodes)
						{
							var allRetargetedNodePathsThatPointToThis = pair.Key.GetAllRetargetedNodePathsThatPointToThis(pair.Value, scene, targetNode);
							var allRetargetedNodePathsThatThisPointsTo = pair.Key.GetAllRetargetedNodePathsThatThisPointsTo(pair.Value, scene, targetNode);
							if (allRetargetedNodePathsThatPointToThis.Count == 0 && allRetargetedNodePathsThatThisPointsTo.Count == 0)
							{
								continue;
							}
							System.Collections.Generic.Dictionary<TscnObject, List<NodePathRetargetMap>> retargets = new();
							foreach (var retargetPair in allRetargetedNodePathsThatPointToThis)
							{
								retargets.Add(retargetPair.Key, retargetPair.Value);
							}
							foreach (var retargetPair in allRetargetedNodePathsThatThisPointsTo)
							{
								retargets.Add(retargetPair.Key, retargetPair.Value);
							}
							retargetValuesMap.Add(new() { RetargetedNodePaths = retargets });
						}
					}

					if (retargetValuesMap.Count > 0)
					{
						contentToReplace[fileData] = retargetValuesMap;
					}

					scene.QueueFree();
				}
			}
		}

		// Do FileData string manipulation in parallel

		// One thing that throws a wrench into all of this is that it turns out that the order of the Nodes and Resources matter.
		// According to https://docs.godotengine.org/en/stable/contributing/development/file_formats/tscn.html#internal-resources,
		// "Some internal resources contain links to other internal resources (such as a mesh having a material).
		// In this case, the referring resource must appear before the reference to it. This means that order matters in the file's internal resources section."
		// Essentially, that means that the Node hierarchy more-or-less needs to be in the right order.
		// For instance, if I want to reparent a Node to another Node but that Node appears underneath it in the hierarchy, then the references will break.
		Parallel.ForEach(contentToReplace, (KeyValuePair<FileData, List<RetargetValueMap>> content) =>
		{
			StringBuilder stringBuilder = new(content.Key.OldFileContent.Length);
			string[] lines = content.Key.OldFileContent.Split('\n');
			System.Collections.Generic.Dictionary<TscnObject, List<NodePathRetargetMap>> allTscnObjectsToChange = new();
			content.Value.ForEach((retargetValueMap) =>
			{
				foreach (var pair in retargetValueMap.RetargetedNodePaths)
				{
					if (allTscnObjectsToChange.ContainsKey(pair.Key))
					{
						allTscnObjectsToChange[pair.Key].AddRange(pair.Value);
					}
					else
					{
						allTscnObjectsToChange[pair.Key] = pair.Value;
					}
				}
			});

			System.Collections.Generic.Dictionary<string, List<(string path, string name, List<string> lines)>> tscnSections = new();
			List<string> currentSection = new();
			TscnObject currentFoundTscnObject = null;
			for (int i = 0; i < lines.Length; i++)
			{
				string line = lines[i];
				string newLine = line;
				if (string.IsNullOrWhiteSpace(line))
				{
					if (currentSection.Count > 0)
					{
						currentSection.Add(newLine);
						void AddToTscnSections(string key, string path, string name, List<string> section)
						{
							if (tscnSections.ContainsKey(key))
							{
								tscnSections[key].Add((path, name, section));
							}
							else
							{
								tscnSections[key] = new() { { (path, name, section) } };
							}
						}
						if (currentSection[0].StartsWith("[gd_scene load_steps="))
						{
							AddToTscnSections("gd_scene", null, null, currentSection);
						}
						else if (currentSection[0].StartsWith("[ext_resource type=\""))
						{
							AddToTscnSections("ext_resource", null, null, currentSection);
						}
						else if (currentSection[0].StartsWith("[sub_resource type=\""))
						{
							AddToTscnSections("sub_resource", null, null, currentSection);
						}
						else if (currentSection[0].StartsWith("[node name=\""))
						{
							int nodeNameStart = "[node name=\"".Length;
							int nodeNameEnd = currentSection[0].IndexOf('"', nodeNameStart);
							string nodeName = currentSection[0][nodeNameStart..nodeNameEnd];
							int parentIndex = currentSection[0].IndexOf("parent=\"");
							int parentNameStart = parentIndex == -1 ? -1 : currentSection[0].IndexOf('"', parentIndex + "parent=".Length) + 1;
							int parentNameEnd = parentNameStart == -1 ? -1 : currentSection[0].IndexOf('"', parentNameStart);
							string pathName = parentNameStart == -1 || parentNameEnd == -1 ? string.Empty : currentSection[0][parentNameStart..parentNameEnd];
							AddToTscnSections("node", pathName, nodeName, currentSection);
						}
						else if (currentSection[0].StartsWith("[editable path=\""))
						{
							AddToTscnSections("editable", null, null, currentSection);
						}
						currentSection = new();
					}
					currentFoundTscnObject = null;
					continue;
				}
				if (currentFoundTscnObject == null)
				{
					if (allTscnObjectsToChange.Count > 0)
					{
						foreach (var pair in allTscnObjectsToChange)
						{
							if (line.StartsWith($@"[sub_resource type=""Resource"" id=""")
							 && pair.Key is TscnResource tscnResource
							 && line.Contains($@"id=""{tscnResource.Id}"""))
							{
								currentFoundTscnObject = tscnResource;
								break;
							}
							else if (line.StartsWith($@"[node name=""")
								  && pair.Key is TscnNode tscnNodeWithParent
								  && string.IsNullOrWhiteSpace(tscnNodeWithParent.Parent) == false
								  && line.Contains($@"name=""{tscnNodeWithParent.Name}""")
								  && line.Contains($@"parent=""{tscnNodeWithParent.Parent}"""))
							{
								currentFoundTscnObject = tscnNodeWithParent;
								break;
							}
							else if (line.StartsWith($@"[node name=""")
								  && pair.Key is TscnNode tscnNodeRoot
								  && string.IsNullOrWhiteSpace(tscnNodeRoot.Parent) == true
								  && line.Contains($@"name=""{tscnNodeRoot.Name}""")
								  && line.Contains($@"parent=""") == false)
							{
								currentFoundTscnObject = tscnNodeRoot;
								break;
							}
						}
					}
					if (line.Contains($"{oldPath}"))
					{
						newLine = line.Replace($"{oldPath}", $"{newParentPath}/{targetNodeName}");
					}
					if (line.Contains($@"[node name=""{targetNodeName}"""))
					{
						int oldParentPathPosition = line.LastIndexOf($"{oldParentPath}\"");
						newLine = line.Remove(oldParentPathPosition, oldParentPath.Length).Insert(oldParentPathPosition, newParentPath);
					}
				}
				if (currentFoundTscnObject != null)
				{
					foreach (NodePathRetargetMap map in allTscnObjectsToChange[currentFoundTscnObject])
					{
						if (line.Contains($@"NodePath(""{map.OldPath}"""))
						{
							newLine = line.Replace($@"NodePath(""{map.OldPath}""", $@"NodePath(""{map.NewPath}""");
						}
						break;
					}
				}
				if (currentSection.Count > 0 || line.StartsWith("[gd_scene load_steps=") || line.StartsWith("[ext_resource type=\"") || line.StartsWith("[sub_resource type=\"") || line.StartsWith("[node name=\"") || line.StartsWith("[editable path=\""))
				{
					currentSection.Add(newLine);
					continue;
				}
				else if (i == lines.Length - 2)
				{
					//stringBuilder.Append(newLine);
				}
				else
				{
					//stringBuilder.AppendLine(newLine);
				}
			}
			List<(string path, string name, List<string> lines)> unorderedSections = new();
			List<(string path, string name, List<string> lines)> reorderedSections = new();
			if (tscnSections.Count > 0)
			{
				foreach (var pair in tscnSections)
				{
					unorderedSections.AddRange(pair.Value);
				}
				foreach (var pair in tscnSections)
				{
					// For all non-nodes, just go ahead and add all of them. The order of them doesn't matter (hopefully lol)
					if (pair.Key != "node")
					{
						reorderedSections.AddRange(pair.Value);
						pair.Value.ForEach((tuple) => unorderedSections.Remove(tuple));
						continue;
					}
					if (pair.Key == "node")
					{
						var nodeSections = tscnSections["node"];

						while (unorderedSections.Count((tuple) => tuple.name != null) > 0)
						{
							for (int i = 0; i < unorderedSections.Count; i++)
							{
								var nodeSection = unorderedSections[i];
								if (nodeSection.name == null)
								{
									continue;
								}
								// If I'm the root...
								if (string.IsNullOrWhiteSpace(nodeSection.path))
								{
									reorderedSections.Add((nodeSection.path, nodeSection.name, nodeSection.lines));
									unorderedSections.RemoveAt(i);
									i--;
									continue;
								}
								// If I'm one down from the root...
								else if (nodeSection.path == ".")
								{
									reorderedSections.Add((nodeSection.path, nodeSection.name, nodeSection.lines));
									unorderedSections.RemoveAt(i);
									i--;
									continue;
								}
								// If my own path starts with any other path still remaining in unorderedSections...
								else if (unorderedSections.Any((tuple) => nodeSection.path.StartsWith($"{tuple.path}/{tuple.name}")))
								{
									continue;
								}
								else
								{
									reorderedSections.Add((nodeSection.path, nodeSection.name, nodeSection.lines));
									unorderedSections.Remove((nodeSection.path, nodeSection.name, nodeSection.lines));
									i--;
									continue;
								}
							}
						}
					}
				}
			}

			for (int reorderedSectionsIndex = 0; reorderedSectionsIndex < reorderedSections.Count; reorderedSectionsIndex++)
			{
				List<string> reorderedSectionLines = reorderedSections[reorderedSectionsIndex].lines;
				for (int linesIndex = 0; linesIndex < reorderedSectionLines.Count; linesIndex++)
				{
					if (reorderedSectionsIndex == reorderedSections.Count - 1 && linesIndex == reorderedSectionLines.Count - 2)
					{
						stringBuilder.Append(reorderedSectionLines[linesIndex].Trim());
					}
					else
					{
						stringBuilder.AppendLine(reorderedSectionLines[linesIndex].Trim());
					}
				}
			}
			content.Key.NewFileContent = stringBuilder.ToString();
		});

		//Probably don't want to do I/O operations in parallel unfortunately
		foreach (KeyValuePair<FileData, List<RetargetValueMap>> content in contentToReplace)
		{
			if (content.Key.OldFileContent != content.Key.NewFileContent)
			{
				using (FileAccess writeFile = FileAccess.Open(content.Key.FilePath, FileAccess.ModeFlags.Write))
				{
					writeFile.StoreString(content.Key.NewFileContent);
				}
			}
		}

		ProgressLabel.Text = "Done!";
	}
}

public static class ReparentNodeRecursivelyHelpers
{
	public record TscnSection(string UniqueName, List<string> Content);

	public abstract record TscnObject()
	{
		public abstract string UniqueName { get; }
	}

	public record TscnResource(string Id) : TscnObject
	{
		public override string UniqueName => Id;
	}

	public record TscnNode(string Name, string Parent) : TscnObject
	{
		public override string UniqueName => string.IsNullOrWhiteSpace(Parent) ? Name : $"{Parent}/{Name}";
	}

	public record NodePathRetargetMap(NodePath OldPath, NodePath NewPath);

	public class RetargetValueMap
	{
		public IDictionary<TscnObject, List<NodePathRetargetMap>> RetargetedNodePaths { get; set; } = default!;
		//public IDictionary<TscnObject, NodePathRetargetMap> RetargetedNodePathsThatPointToThis { get; set; }
		//public IDictionary<TscnObject, List<NodePathRetargetMap>> RetargetedNodePathsThatThisPointsTo { get; set; }
	}

	/// <summary>
	/// This will return the path in the same format that it would if it were in a .tscn file (the Root has no parent, top-level children are just a ".", while anything below that is the regular path)
	/// </summary>
	public static string GetTscnPathTo(this Node sceneRoot, Node otherNode)
	{
		if (otherNode == sceneRoot)
		{
			return string.Empty;
		}
		if (sceneRoot.GetChildren().Contains(otherNode))
		{
			return ".";
		}
		return sceneRoot.GetPathTo(otherNode.GetParent());
	}

	/// <summary> A copy must be done because Node.Reparent does not update NodePaths for some reason... Not even Node.RemoveChild() and then Node.AddChild() updates NodePaths. </summary>
	public static IDictionary<Node, Node> CopyNodeStructure(this Node thisNode, Node newParent)
	{
		System.Collections.Generic.Dictionary<Node, Node> oldNewMap = new();
		Node newNode = new() { Name = thisNode.Name };
		newParent.AddChild(newNode);
		oldNewMap.Add(thisNode, newNode);
		foreach (Node child in thisNode.GetChildren())
		{
			foreach (KeyValuePair<Node, Node> oldNewPair in child.CopyNodeStructure(newNode))
			{
				oldNewMap.Add(oldNewPair.Key, oldNewPair.Value);
			}
		}
		return oldNewMap;
	}

	public static IDictionary<(Node owningNode, TscnObject tscnObject), NodePath> GetAllNodePathsThatPointToThis(this Node thisNode, Node sceneRoot)
	{
		System.Collections.Generic.Dictionary<(Node owningNode, TscnObject tscnObject), NodePath> references = new();

		IDictionary<(Node owningNode, TscnObject tscnObject), NodePath> TestProperties(GodotObject godotObject, Node owningNode)
		{
			System.Collections.Generic.Dictionary<(Node owningNode, TscnObject tscnObject), NodePath> properties = null;
			foreach (Dictionary property in godotObject.GetPropertyList())
			{
				StringName propertyName = property["name"].AsStringName();
				Variant propertyValue = godotObject.Get(propertyName);

				void TestProperty(Variant inProperty)
				{
					if (inProperty.VariantType == Variant.Type.NodePath)
					{
						NodePath nodePath = inProperty.AsNodePath();
						Node foundNode = owningNode.GetNodeOrNull(nodePath);
						if (foundNode == thisNode)
						{
							TscnObject tscnObject = godotObject switch
							{
								Resource resource => new TscnResource(resource.ResourcePath.Split("::").LastOrDefault()!),
								Node node => new TscnNode(node.Name, sceneRoot.GetTscnPathTo(node)),
								_ => null
							};
							properties ??= new();
							if (properties.ContainsKey((owningNode, tscnObject)))
							{
								properties[(owningNode, tscnObject)] = nodePath;
							}
							else
							{
								properties.Add((owningNode, tscnObject), nodePath);
							}
						}
					}
					else if (inProperty.VariantType == Variant.Type.Object)
					{
						GodotObject propertyAsObject = inProperty.AsGodotObject();
						if (propertyAsObject == thisNode)
						{
							TscnNode tscnNode = new(godotObject.As<Node>()!.Name, sceneRoot.GetTscnPathTo(godotObject.As<Node>()!));
							properties ??= new();
							if (properties.ContainsKey(new(owningNode, tscnNode)))
							{
								properties[(owningNode, tscnNode)] = (godotObject.As<Node>()!).GetPathTo(thisNode);
							}
							else
							{
								properties.Add((owningNode, tscnNode), (godotObject.As<Node>()!).GetPathTo(thisNode));
							}
						}
						else if (propertyAsObject is Resource resource)
						{
							IDictionary<(Node owningNode, TscnObject tscnObject), NodePath> foundProperties = TestProperties(resource, owningNode);
							if (foundProperties != null && foundProperties.Count > 0)
							{
								properties ??= new();
								foreach (KeyValuePair<(Node owningNode, TscnObject tscnObject), NodePath> pair in foundProperties)
								{
									if (properties.ContainsKey(pair.Key))
									{
										properties[pair.Key] = pair.Value;
									}
									else
									{
										properties.Add(pair.Key, pair.Value);
									}
								}
							}
						}
					}
					else if (inProperty.VariantType == Variant.Type.Dictionary)
					{
						Dictionary propertyDictionary = inProperty.AsGodotDictionary();
						foreach (KeyValuePair<Variant, Variant> variantPair in propertyDictionary)
						{
							TestProperty(variantPair.Key);
							TestProperty(variantPair.Value);
						}
					}
					else if (inProperty.VariantType == Variant.Type.Array)
					{
						Godot.Collections.Array propertyArray = inProperty.AsGodotArray();
						foreach (Variant variant in propertyArray)
						{
							TestProperty(variant);
						}
					}
				}

				TestProperty(propertyValue);
			}
			return properties;
		}

		foreach (Node node in sceneRoot.Enumerate())
		{
			if (node == thisNode)
			{
				continue;
			}
			IDictionary<(Node owningNode, TscnObject tscnObject), NodePath> foundProperties = TestProperties(node, node);
			if (foundProperties != null && foundProperties.Count > 0)
			{
				foreach (KeyValuePair<(Node owningNode, TscnObject tscnObject), NodePath> foundPair in foundProperties)
				{
					references.Add(foundPair.Key, foundPair.Value);
				}
			}
		}
		return references;
	}

	public static IDictionary<TscnObject, List<NodePathRetargetMap>> GetAllRetargetedNodePathsThatPointToThis(this Node thisNode, Node newTargetNode, Node sceneRoot, Node baseNodeToBeReparented)
	{
		// Note that, with any node that is a direct child of thisNode (use thisNode.IsChildOf()), if any of their Paths are to any other children of thisNode, they need to be skipped
		// The length of the List in this instance will always be one. Just returning a list to make it simpler to merge these values with the ones from GetAllRetargetedNodePathsThatThisPointsto

		System.Collections.Generic.Dictionary<TscnObject, List<NodePathRetargetMap>> retargetedReferences = new();

		IDictionary<(Node owningNode, TscnObject tscnObject), NodePath> allNodePathsThatPointToThis = thisNode.GetAllNodePathsThatPointToThis(sceneRoot);

		foreach (KeyValuePair<(Node owningNode, TscnObject tscnObject), NodePath> pair in allNodePathsThatPointToThis)
		{
			// In other words, if the node is a child of the base Node that is being reparented, then skip me
			if (pair.Key.owningNode.IsAChildOf(baseNodeToBeReparented) || pair.Key.owningNode == baseNodeToBeReparented)
			{
				continue;
			}

			retargetedReferences.Add(pair.Key.tscnObject, new() { { new(pair.Value, $"{pair.Key.owningNode.GetPathTo(newTargetNode)}") } });
		}

		return retargetedReferences;
	}

	// The Key is the .tscn identifier for the Node or Resource. If it's a sub-resource, it'll just be the sub-resource ID. Otherwise, if it's a Node, then it's the full path, including its name (this will let me find the value)
	// The Value is the array of NodePaths that the Key points to.
	// We do not need the variable names at all anymore as we only care about what the NodePath is
	public static IDictionary<TscnObject, Array<NodePath>> GetAllNodePathsThatThisPointsTo(this Node inNode, Node root)
	{
		System.Collections.Generic.Dictionary<TscnObject, Array<NodePath>> TestProperties(GodotObject godotObject)
		{
			System.Collections.Generic.Dictionary<TscnObject, Array<NodePath>> properties = null;
			foreach (Dictionary property in godotObject.GetPropertyList())
			{
				StringName propertyName = property["name"].AsStringName();
				Variant propertyValue = godotObject.Get(propertyName);

				if (propertyName.Equals("owner"))
				{
					continue;
				}

				void TestProperty(Variant inProperty)
				{
					if (inProperty.VariantType == Variant.Type.NodePath)
					{
						NodePath nodePath = inProperty.AsNodePath();
						Node foundNode = inNode.GetNodeOrNull(nodePath);
						if (foundNode != null)
						{
							TscnObject tscnObject = godotObject switch
							{
								Resource resource => new TscnResource(resource.ResourcePath.Split("::").LastOrDefault()),
								Node node => new TscnNode(node.Name, root.GetTscnPathTo(node)),
								_ => null
							};
							properties ??= new();
							if (properties.ContainsKey(tscnObject))
							{
								properties[tscnObject].Add(nodePath);
							}
							else
							{
								properties.Add(tscnObject, new() { { nodePath } });
							}
						}
					}
					else if (inProperty.VariantType == Variant.Type.Object)
					{
						GodotObject propertyAsObject = inProperty.AsGodotObject();
						if (propertyAsObject is Node node)
						{
							TscnNode tscnNode = new((godotObject as Node).Name, root.GetTscnPathTo(godotObject as Node));
							properties ??= new();
							if (properties.ContainsKey(tscnNode))
							{
								properties[tscnNode].Add((godotObject as Node).GetPathTo(node));
							}
							else
							{
								properties.Add(tscnNode, new() { { (godotObject as Node).GetPathTo(node) } });
							}
						}
						else if (propertyAsObject is Resource resource)
						{
							System.Collections.Generic.Dictionary<TscnObject, Array<NodePath>> foundProperties = TestProperties(resource);
							if (foundProperties != null && foundProperties.Count > 0)
							{
								properties ??= new();
								foreach (KeyValuePair<TscnObject, Array<NodePath>> foundPropertiesPair in foundProperties)
								{
									foreach (NodePath nodePath in foundPropertiesPair.Value)
									{
										if (properties.ContainsKey(foundPropertiesPair.Key))
										{
											properties[foundPropertiesPair.Key].Add(nodePath);
										}
										else
										{
											properties.Add(foundPropertiesPair.Key, new() { { nodePath } });
										}
									}
								}
							}
						}
					}
					else if (inProperty.VariantType == Variant.Type.Dictionary)
					{
						Dictionary propertyDictionary = inProperty.AsGodotDictionary();
						foreach (KeyValuePair<Variant, Variant> variantPair in propertyDictionary)
						{
							TestProperty(variantPair.Key);
							TestProperty(variantPair.Value);
						}
					}
					else if (inProperty.VariantType == Variant.Type.Array)
					{
						Godot.Collections.Array propertyArray = inProperty.AsGodotArray();
						foreach (Variant variant in propertyArray)
						{
							TestProperty(variant);
						}
					}
				}

				TestProperty(propertyValue);
			}
			return properties;
		}

		return TestProperties(inNode);
	}

	public static IDictionary<TscnObject, List<NodePathRetargetMap>> GetAllRetargetedNodePathsThatThisPointsTo(this Node thisNode, Node newTargetNode, Node root, Node baseNodeToBeRetargeted)
	{
		// Note that, with any node that is a direct child of thisNode (use thisNode.IsChildOf()), if any of their Paths are to any other children of thisNode, they need to be skipped

		System.Collections.Generic.Dictionary<TscnObject, List<NodePathRetargetMap>> retargetedReferences = new();

		IDictionary<TscnObject, Array<NodePath>> allNodePathsThatPointToThis = thisNode.GetAllNodePathsThatThisPointsTo(root);

		if (allNodePathsThatPointToThis == null) { return retargetedReferences; }
		foreach (KeyValuePair<TscnObject, Array<NodePath>> pair in allNodePathsThatPointToThis)
		{
			List<NodePathRetargetMap> retargetedPaths = new();
			foreach (NodePath oldPath in pair.Value)
			{
				// In other words, if the oldPath points to a child of the base Node that is being reparented, then skip me
				Node oldNode = thisNode.GetNode(oldPath);
				if (oldNode.IsAChildOf(baseNodeToBeRetargeted) || oldNode == baseNodeToBeRetargeted)
				{
					continue;
				}
				retargetedPaths.Add(new(oldPath, newTargetNode.GetPathTo(oldNode)));
			}
			if (retargetedPaths.Count > 0)
			{
				retargetedReferences.Add(pair.Key, retargetedPaths);
			}
		}

		return retargetedReferences;
	}


	// Everything under here is currently unused but it's definitely within the realm of possibilty that they will be used just to simplify things

	// This will get all properties for all Nodes under root that target oldNode and get the path for them as if they were to target newNode.
	// In addition to this, if the newNode has any targets, these will be updated in a similar fashion.
	// The Key of the first Dictionary is the ID for the Node or Resource. If it's a sub-resource, it'll just be the sub-resource ID. Otherwise, if it's a Node, then it's the full path, including its name.
	// The Key of the sub-Dictionary is the old NodePath value. The Value of the sub-Dictionary is the new, re-targeted NodePath value. Each of these values would need to be replaced with a RegEx.Replace().
	public static Godot.Collections.Dictionary<StringName, Godot.Collections.Dictionary<NodePath, NodePath>> GetAllRetargetedNodePaths(Node oldNode, Node newNode, Node root)
	{
		Godot.Collections.Dictionary<StringName, Godot.Collections.Dictionary<NodePath, NodePath>> references = new();
		foreach (Node node in root.Enumerate())
		{
			Godot.Collections.Dictionary<StringName, NodePath> TestProperties(GodotObject inObject)
			{
				Godot.Collections.Dictionary<StringName, NodePath> properties = null;

				foreach (Dictionary property in node.GetPropertyList())
				{
					StringName propertyName = property["name"].AsStringName();
					Variant propertyValue = node.Get(propertyName);


				}

				return properties;
			}


			if (node == oldNode)
			{

			}

			TestProperties(node);
		}
		return null;
	}

	public static void RetargetNodePaths(Node oldNode, Node newNode, GodotObject godotObject, Node owningNode)
	{
		foreach (Dictionary property in godotObject.GetPropertyList())
		{
			StringName propertyName = property["name"].AsStringName();
			Variant propertyValue = godotObject.Get(propertyName);

			void TestProperties(Variant inProperty)
			{
				if (inProperty.VariantType == Variant.Type.NodePath)
				{
					Node foundNode = owningNode?.GetNode(inProperty.AsNodePath());
					if (foundNode == oldNode)
					{
						godotObject.Set(propertyName, newNode);
					}
				}
				else if (inProperty.VariantType == Variant.Type.Object)
				{
					GodotObject propertyAsObject = inProperty.AsGodotObject();
					if (propertyAsObject == oldNode)
					{
						godotObject.Set(propertyName, newNode);
					}
					else if (propertyAsObject is Resource resource)
					{
						RetargetNodePaths(oldNode, newNode, propertyAsObject, owningNode);
					}
				}
				else if (inProperty.VariantType == Variant.Type.Dictionary)
				{
					Dictionary propertyDictionary = inProperty.AsGodotDictionary();
					foreach (KeyValuePair<Variant, Variant> variantPair in propertyDictionary)
					{
						TestProperties(variantPair.Value);
					}
				}
				else if (inProperty.VariantType == Variant.Type.Array)
				{
					Godot.Collections.Array propertyArray = inProperty.AsGodotArray();
					foreach (Variant variant in propertyArray)
					{
						TestProperties(variant);
					}
				}
			}

			TestProperties(propertyValue);
		}
	}
}
