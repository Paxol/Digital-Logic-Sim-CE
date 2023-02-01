namespace Assets.Scripts.Chip.Interfaces
{
  internal interface ICustomSaveLogic
  {
    void HandleSaveMetadata(SavedComponentChip savedChip);
    void HandleLoad(SavedComponentChip savedChip);
  }
}
