const AdminSocialDetailPage = ({
  params,
}: {
  readonly params: Promise<{ socialId: string }>;
}): React.JSX.Element => {
  void params;
  return (
    <div>
      <h1 className="text-3xl font-bold">Social Event Admin</h1>
      <p className="text-muted-foreground mt-2">Manage social event details and participants.</p>
    </div>
  );
};

export default AdminSocialDetailPage;
