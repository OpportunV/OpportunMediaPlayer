namespace OMP.Ui.Models;

internal interface IAlignmentOption<out TAlignment>
    where TAlignment : struct
{
    public TAlignment Value { get; }

    public bool IsSelected { get; set; }
}
