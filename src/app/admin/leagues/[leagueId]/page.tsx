const AdminLeagueDetailPage = ({
  params,
}: {
  readonly params: Promise<{ leagueId: string }>;
}): React.JSX.Element => {
  void params;
  return (
    <div>
      <h1 className="text-3xl font-bold">League Admin</h1>
      <p className="text-muted-foreground mt-2">Manage league settings, players, and stages.</p>
    </div>
  );
};

export default AdminLeagueDetailPage;
