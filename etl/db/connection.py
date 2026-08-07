from contextlib import contextmanager
from sqlalchemy import create_engine
from sqlalchemy.orm import sessionmaker
from sqlalchemy.pool import NullPool

from config import settings

# Operational DB (porms_etl role)
_operational_engine = create_engine(
    settings.POSTGRES_DSN,
    poolclass=NullPool,
    connect_args={"options": "-c search_path=operational,public"},
    echo=False,
)
OperationalSession = sessionmaker(bind=_operational_engine)

@contextmanager
def get_operational_session():
    session = OperationalSession()
    try:
        yield session
        session.commit()
    except Exception:
        session.rollback()
        raise
    finally:
        session.close()
