using System.Collections.Generic;
using System.Threading.Tasks;

namespace AssetInventory
{
    public abstract class Validator
    {
        public enum ValidatorType
        {
            DB,
            FileSystem
        }

        public enum State
        {
            Idle,
            Scanning,
            Completed,
            Fixing
        }

        public ValidatorType Type { get; set; }
        public State CurrentState { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public bool Fixable { get; set; } = true;
        public List<AssetInfo> DBIssues { get; set; }
        public List<string> FileIssues { get; set; }

        public int IssueCount { get { return Type == ValidatorType.DB ? DBIssues.Count : FileIssues.Count; } }

        public abstract Task Validate();
        public abstract Task Fix();
    }
}
