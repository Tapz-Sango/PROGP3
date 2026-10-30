namespace SmartX.Models
{
    public class DeploymentNode
    {
        public string Name { get; set; }
        public string Level { get; set; }
        public bool IsConfiguredSafely { get; set; }
        public DeploymentNode? Parent { get; set; }

        public DeploymentNode(string name, string level, bool isSafe, DeploymentNode? parent = null)
        {
            Name = name;
            Level = level;
            IsConfiguredSafely = isSafe;
            Parent = parent;
        }

        // Walks up the parent chain and checks that every node is safely configured. Returns true if the whole hierarchy is safe, otherwise will return false with the reason.
        public static bool RecursiveValidateHierarchy(DeploymentNode? node, out string failureReason)
        {
            // Reached the top of the chain, nothing left to check
            if (node == null)
            {
                failureReason = "Safe";
                return true;
            }

            // Stops as soon as an unsafe node is found
            if (!node.IsConfiguredSafely)
            {
                failureReason = $"Hierarchy Security Fault at: [{node.Level}] {node.Name}";
                return false;
            }

            // Keep climbing up to the parent
            return RecursiveValidateHierarchy(node.Parent, out failureReason);
        }
    }
}