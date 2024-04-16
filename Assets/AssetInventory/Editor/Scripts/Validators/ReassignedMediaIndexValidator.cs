using System.Threading.Tasks;

namespace AssetInventory
{
    public sealed class ReassignedMediaIndexValidator : Validator
    {
        public ReassignedMediaIndexValidator()
        {
            Type = ValidatorType.DB;
            Name = "Reassigned Media Index";
            Description = "Checks if media files were previously stored with no package assignment but have one in the meantime and will remove the references without package assignment.";
        }

        public override async Task Validate()
        {
            CurrentState = State.Scanning;

            DBIssues = await AssetInventory.GatherReassignedMediaIndexes();

            CurrentState = State.Completed;
        }

        public override async Task Fix()
        {
            CurrentState = State.Fixing;

            await AssetInventory.RemoveReassignedMediaIndexes(DBIssues);

            await Validate();
        }
    }
}
