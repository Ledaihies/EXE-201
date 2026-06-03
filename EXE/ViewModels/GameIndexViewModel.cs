using System.Collections.Generic;

namespace EXE.ViewModels
{
	public class GameIndexViewModel
	{
		public List<GameProductDto> Products { get; set; } = new();
	}

	public class GameProductDto
	{
		public int ProductId { get; set; }
		public string Name { get; set; } = string.Empty;
		public string? Description { get; set; }
		public decimal? Price { get; set; }
		public string? Category { get; set; }
		public string? Region { get; set; }
		public string? Province { get; set; }
		public string ImageUrl { get; set; } = "no-image.svg";
	}
}

