type AdvancesHeroProps = {
  onImportTemplate: () => void
}

const heroNotes = [
  {
    title: 'Nhập nhanh',
    detail: 'Ưu tiên 4 trường cốt lõi trước.',
  },
  {
    title: 'Xử lý tại dòng',
    detail: 'Duyệt, hủy hoặc bỏ hủy ngay trong worklist.',
  },
  {
    title: 'Import đúng chỗ',
    detail: 'Đi sang batch import khi cần thao tác theo file.',
  },
]

export default function AdvancesHero({ onImportTemplate }: AdvancesHeroProps) {
  return (
    <section className="card advances-hero" aria-labelledby="advances-hero-title">
      <div className="advances-hero__main">
        <div className="advances-hero__content">
          <span className="advances-hero__eyebrow">Khoản trả hộ KH</span>
          <h2 id="advances-hero-title">Workspace nhập liệu và xử lý khoản trả hộ KH</h2>
          <p className="muted advances-hero__lead">
            Giữ phần nhập liệu ở ngay đầu màn hình, chuyển import về đúng luồng batch, và dành phần
            còn lại cho worklist cần rà soát hoặc duyệt nhanh.
          </p>
        </div>

        <div className="advances-hero__notes" aria-label="Điểm nhấn quy trình">
          {heroNotes.map((note) => (
            <div key={note.title} className="advances-hero__note">
              <strong>{note.title}</strong>
              <span className="muted">{note.detail}</span>
            </div>
          ))}
        </div>
      </div>

      <div className="advances-hero__actions">
        <button type="button" className="btn btn-primary" onClick={onImportTemplate}>
          Import từ template
        </button>
        <a className="btn btn-outline" href="#advances-worklist">
          Xem danh sách
        </a>
      </div>
    </section>
  )
}
