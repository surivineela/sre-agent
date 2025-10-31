public interface ILegacyModelConverter<TNewModel>
{
    TNewModel ToNewModel();
}