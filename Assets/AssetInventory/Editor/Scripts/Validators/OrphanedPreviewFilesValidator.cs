using System.Threading.Tasks;

namespace AssetInventory
{
    public sealed class OrphanedPreviewFilesValidator : Validator
    {
        public OrphanedPreviewFilesValidator()
        {
            Type = ValidatorType.FileSystem;
            Name = "Orphaned Preview Files";
            Description = "Scans the file system for preview files that are not referenced anymore.";
        }

        public override async Task Validate()
        {
            CurrentState = State.Scanning;
            FileIssues = await AssetInventory.GatherOrphanedPreviews();
            CurrentState = State.Completed;
        }

        public override async Task Fix()
        {
            CurrentState = State.Fixing;
            await AssetInventory.RemoveOrphanedPreviews(FileIssues);
            CurrentState = State.Idle;
        }
    }
}
