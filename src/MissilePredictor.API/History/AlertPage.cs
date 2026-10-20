namespace MissilePredictor.API.History;

public record AlertPage(IReadOnlyList<NeptunAlert> Items, int Total, int Page, int PageSize);
