const LeagueDetailPage = ({
  params,
}: {
  readonly params: Promise<{ leagueId: string }>;
}): React.JSX.Element => {
  void params;
  return (
    <main className="flex min-h-screen flex-col p-6">
      <h1 className="text-3xl font-bold">League Details</h1>
      <p className="text-muted-foreground mt-2">View league standings, stages, and players.</p>
    </main>
  );
};

export default LeagueDetailPage;
