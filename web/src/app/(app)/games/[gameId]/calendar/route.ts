import { apiWithSession } from "@/lib/api/server";
import { gameCalendarFile } from "@/lib/calendar";
import { isId } from "@/lib/ids";

// Each Game's file is the signed-in Player's, and its Date can change: never cached.
export const dynamic = "force-dynamic";

/**
 * "Add to calendar" for an arranged Game (#9 part 3): its agreed Date as an
 * all-day event in a calendar file. For its own Players, who see it on the
 * Game; anyone else, or a Game with no Date, gets a 404.
 */
export async function GET(_request: Request, { params }: RouteContext<"/games/[gameId]/calendar">) {
  const { gameId } = await params;
  if (!isId(gameId)) {
    return new Response(null, { status: 404 });
  }

  const api = await apiWithSession();
  const { data: game } = await api.GET("/games/{gameId}", { params: { path: { gameId } } });
  if (!game?.date || game.yourPlayerIds.length === 0) {
    return new Response(null, { status: 404 });
  }

  const sides = game.sides.map((side) => side.players.map((player) => player.name).join(" & "));
  const file = gameCalendarFile({
    id: game.id,
    date: game.date,
    summary: `Tennis: ${sides[0]} v ${sides[1]}`,
    description: `${game.leagueName}, ${game.clubName}. Division ${Number(game.division)}, Stage ${Number(game.stage.number)}.`,
    court: game.court,
    now: new Date(),
  });
  return new Response(file, {
    headers: {
      "Content-Type": "text/calendar; charset=utf-8",
      "Content-Disposition": `attachment; filename="game-${game.date}.ics"`,
      "Cache-Control": "private, no-store",
    },
  });
}
