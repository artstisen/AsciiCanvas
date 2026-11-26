namespace AsciiCanvas;

/// <summary>Weight of each arm of a box-drawing character: 0 none, 1 light, 2 heavy, 3 double.</summary>
/// <param name="Up">The weight of the arm towards the top of the cell.</param>
/// <param name="Down">The weight of the arm towards the bottom of the cell.</param>
/// <param name="Left">The weight of the arm towards the left of the cell.</param>
/// <param name="Right">The weight of the arm towards the right of the cell.</param>
public readonly record struct LineSegments(byte Up, byte Down, byte Left, byte Right);
