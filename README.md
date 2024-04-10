# ReparentNodeRecursively

This is a tool that I made in order to drastically simplify the process of reparenting Nodes in a SceneTree in Godot because Godot's editor is incredibly brittle whenever dealing with refactoring, much more so than Unity or Unreal unfortunately,
which cause lots of breakage with NodePath references in other scenes that may reference a .tscn you're wanting to refactor.

Be careful whenever using this because if you don't have unique names for your Nodes, you could inadvertently move much more than what you were expecting to move. Since no IDs are assigned to Nodes at all in Godot's editor (at least right now, I don't know if there are any plans to add that functionality),
the only way to definitevly tell one Node from another with no ambiguity whatsoever is to give that Node something like a fully unique name. That is what this uses in order to find your Nodes in each .tscn file.

In theory, I could just as easily do something like add a string for the Id to the internal metadata Dictionary of each Node but I actually no longer use this tool for my personal projects anymore, instead choosing to use the compile-time safety of C# to deal with most of the refactoring safety for me.

As far as I am aware, so long as your Scene is set up properly with a sub-root node directly underneath the actual root node in the .tscn file and you have your Node names actually unique, then this works for every possible situation.
No matter how deeply nested your node references are, whether they're in nested Dictionaries, nested Arrays, nested Resources, or a combination of all-of-the-above, regardless of the amount of abstractions you may have, this, from my experience, will work as expected.
