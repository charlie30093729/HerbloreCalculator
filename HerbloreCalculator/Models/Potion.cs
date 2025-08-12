namespace HerbloreCalculator.Models
{
    public class Potion
    {
        public string Name { get; set; }
        public int BaseId { get; set; }
        public int SecondaryId { get; set; }
        public int Output3Id { get; set; }
        public int Output4Id { get; set; }
        public double Xp { get; set; }

        public Potion(string name, int baseId, int secondaryId, int output3Id, int output4Id, double xp)
        {
            Name = name;
            BaseId = baseId;
            SecondaryId = secondaryId;
            Output3Id = output3Id;
            Output4Id = output4Id;
            Xp = xp;
        }
    }
}
