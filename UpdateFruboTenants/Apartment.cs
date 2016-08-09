namespace UpdateFruboTenants
{
    public class Apartment
    {
        public int Number { get; set; }
        public int Size { get; set; }
        public string Building { get; set; }
        public Owner[] Owners { get; set; }

        public override string ToString()
        {
            return this.Building + ", " + this.Number.ToString();
        }
    }
}
