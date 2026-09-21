using System.Collections.Generic;

namespace Natori.CityBuilder
{
    public sealed class NatoriCityBuildingValidationResult
    {
        private readonly List<string> _errors = new();
        private readonly List<string> _warnings = new();

        public IReadOnlyList<string> Errors => _errors;
        public IReadOnlyList<string> Warnings => _warnings;
        public bool IsValid => _errors.Count == 0;

        internal void AddError(string error)
        {
            _errors.Add(error);
        }

        internal void AddWarning(string warning)
        {
            _warnings.Add(warning);
        }
    }
}
