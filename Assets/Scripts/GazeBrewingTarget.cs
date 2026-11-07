using UnityEngine;

public class GazeBrewingTarget : MonoBehaviour
{
    public GazeBrewingTable table;
    public int ingredientIndex;
    public Transform GazeRoot => ingredientIndex < 0 ? table.receivingBottle : table.vials[ingredientIndex];
}
